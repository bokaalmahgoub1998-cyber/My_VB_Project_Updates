using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Mahgoub_Store.Models;
using Microsoft.AspNetCore.Components;

namespace Mahgoub_Store.Services;

public sealed class StoreApiClient
{
    private readonly HttpClient _http;
    private readonly CentralRegistryService _registry;
    private readonly NavigationManager _nav;
    private readonly DeviceFingerprintService _fp;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private string _tunnel = "";
    private string _slug = "";
    private string _device = "";
    private string? _logoOnce;

    private readonly StoreBrandHub _brand;
    private StoreBootstrapDto? _cached;
    private string _cachedSlug = "";
    private List<StorePaymentDto>? _payments;
    private readonly SemaphoreSlim _session = new(1, 1);

    public StoreApiClient(
        HttpClient http,
        CentralRegistryService registry,
        NavigationManager nav,
        DeviceFingerprintService fp,
        StoreBrandHub brand)
    {
        _http = http;
        _registry = registry;
        _nav = nav;
        _fp = fp;
        _brand = brand;
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public string ImageUrl(string slug, int itemId)
    {
        _ = slug;
        if (!IsLocalHost() && !string.IsNullOrWhiteSpace(_slug))
            return EdgeUrl($"api/webstore/images/{itemId}");
        return Abs($"api/webstore/images/{itemId}");
    }

    public string LogoUrl(string slug)
    {
        _ = slug;
        if (!string.IsNullOrEmpty(_logoOnce))
            return _logoOnce;
        _logoOnce = !IsLocalHost() && !string.IsNullOrWhiteSpace(_slug)
            ? EdgeUrl("api/webstore/logo")
            : $"{_tunnel}/api/webstore/logo";
        return _logoOnce;
    }

    private string Abs(string relative) =>
        string.IsNullOrWhiteSpace(_tunnel) ? relative : $"{_tunnel}/{relative.TrimStart('/')}";

    public async Task<StoreBootstrapDto?> BootstrapAsync(string slug, CancellationToken ct = default)
    {
        if (string.Equals(_cachedSlug, slug, StringComparison.OrdinalIgnoreCase)
            && _cached is { Available: true })
            return _cached;

        var resolved = await _registry.ResolveAsync(slug, ct);
        if (!resolved.Available || string.IsNullOrWhiteSpace(resolved.TunnelUrl))
        {
            Remember(slug, resolved);
            return resolved;
        }

        _slug = slug;
        _tunnel = resolved.TunnelUrl.TrimEnd('/');
        await EnsureDevice(ct);

        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pingCts.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            using var ping = await DispatchAsync(HttpMethod.Get, "api/webstore/ping", null, pingCts.Token);
            if (!ping.IsSuccessStatusCode || IsHtml(ping))
            {
                var err = await ReadAsync<StoreBootstrapDto>(ping, ct);
                var closed = new StoreBootstrapDto
                {
                    Available = false,
                    Slug = slug,
                    Message = err?.Message
                        ?? (ping.StatusCode == System.Net.HttpStatusCode.Forbidden
                            ? CentralRegistryService.ClosedMessage
                            : CentralRegistryService.OfflineMessage)
                };
                Remember(slug, closed);
                return closed;
            }
        }
        catch
        {
            var offline = new StoreBootstrapDto
            {
                Available = false,
                Slug = slug,
                Message = CentralRegistryService.OfflineMessage
            };
            Remember(slug, offline);
            return offline;
        }

        try
        {
            using var brand = await DispatchAsync(HttpMethod.Get, "api/webstore/branding", null, ct);
            if (brand.IsSuccessStatusCode && !IsHtml(brand))
            {
                var b = await brand.Content.ReadFromJsonAsync<StoreBootstrapDto>(Json, ct);
                if (b is not null)
                {
                    if (!string.IsNullOrWhiteSpace(b.Name))
                        resolved.Name = b.Name;
                    resolved.PrimaryColor = StoreBrandCss.NormalizeHex(b.PrimaryColor);
                    resolved.HasLogo = b.HasLogo;
                    resolved.DiscountPercent = b.DiscountPercent;
                    resolved.MapText = b.MapText ?? "";
                    resolved.Restaurant = b.Restaurant;
                    if (b.HasLogo)
                        resolved.LogoUrl = LogoUrl(slug);
                }
            }
        }
        catch
        {
            /* الاسم/الشعار/اللون من الجهاز الرئيسي بعد نجاح الاتصال */
        }

        resolved.Available = true;
        resolved.TunnelUrl = _tunnel;
        Remember(slug, resolved);
        return resolved;
    }

    private void Remember(string slug, StoreBootstrapDto dto)
    {
        if (!string.Equals(_cachedSlug, slug, StringComparison.OrdinalIgnoreCase))
            _payments = null;
        if (dto.Available)
        {
            _cachedSlug = slug;
            _cached = dto;
        }
        _brand.Publish(dto);
    }

    public async Task EnsureSessionAsync(string slug, CancellationToken ct = default)
    {
        if (string.Equals(_cachedSlug, slug, StringComparison.OrdinalIgnoreCase)
            && _cached is { Available: true }
            && _payments is not null)
            return;

        await _session.WaitAsync(ct);
        try
        {
            if (string.Equals(_cachedSlug, slug, StringComparison.OrdinalIgnoreCase)
                && _cached is { Available: true }
                && _payments is not null)
                return;

            var boot = await BootstrapAsync(slug, ct);
            if (boot is not { Available: true })
                return;
            if (_payments is null)
                _payments = await LoadPaymentsAsync(slug, ct);
        }
        finally
        {
            _session.Release();
        }
    }

    public IReadOnlyList<StorePaymentDto> SessionPayments => _payments ?? [];

    public async Task<StoreCatalogDto?> CatalogAsync(string slug, int skip = 0, int take = 24, string? q = null, int cat = 0, CancellationToken ct = default)
    {
        await EnsureTunnel(slug, ct);
        var path = $"api/webstore/catalog?skip={skip}&take={take}";
        if (!string.IsNullOrWhiteSpace(q))
            path += "&q=" + Uri.EscapeDataString(q.Trim());
        if (cat > 0)
            path += "&cat=" + cat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var resp = await DispatchAsync(HttpMethod.Get, path, null, ct);
        if (!resp.IsSuccessStatusCode || IsHtml(resp))
            return null;
        try { return await resp.Content.ReadFromJsonAsync<StoreCatalogDto>(Json, ct); }
        catch { return null; }
    }

    public async Task<List<StorePaymentDto>> PaymentsAsync(string slug, CancellationToken ct = default)
    {
        if (_payments is not null
            && string.Equals(_cachedSlug, slug, StringComparison.OrdinalIgnoreCase))
            return _payments;
        _payments = await LoadPaymentsAsync(slug, ct);
        return _payments;
    }

    private async Task<List<StorePaymentDto>> LoadPaymentsAsync(string slug, CancellationToken ct)
    {
        await EnsureTunnel(slug, ct);
        using var resp = await DispatchAsync(HttpMethod.Get, "api/webstore/payments", null, ct);
        if (!resp.IsSuccessStatusCode)
            return [];
        return await resp.Content.ReadFromJsonAsync<List<StorePaymentDto>>(Json, ct) ?? [];
    }

    public async Task<(SubmitOrderResult? Ok, string? Error)> SubmitAsync(string slug, SubmitOrderRequest body, CancellationToken ct = default)
    {
        await EnsureTunnel(slug, ct);
        await EnsureDevice(ct);
        body.ClientFingerprint = _device;
        byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
        using var resp = await DispatchAsync(HttpMethod.Post, "api/webstore/orders", json, ct);
        var parsed = await ReadAsync<SubmitOrderResult>(resp, ct);
        if (resp.IsSuccessStatusCode)
            return (parsed, null);
        if (parsed?.UnavailableItemIds is { Count: > 0 } ids)
            return (null, "UNAVAILABLE:" + string.Join(",", ids));
        return (null, parsed?.Message ?? "تعذر إرسال الطلب. حاول لاحقاً.");
    }

    public async Task<OrderStatusDto?> StatusAsync(string slug, Guid orderId, string token, CancellationToken ct = default)
    {
        await EnsureTunnel(slug, ct);
        using var resp = await DispatchAsync(
            HttpMethod.Get,
            $"api/webstore/orders/{orderId:D}?token={Uri.EscapeDataString(token)}",
            null,
            ct);
        if (!resp.IsSuccessStatusCode)
            return null;
        return await resp.Content.ReadFromJsonAsync<OrderStatusDto>(Json, ct);
    }

    private async Task EnsureTunnel(string slug, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_tunnel) && string.Equals(_slug, slug, StringComparison.OrdinalIgnoreCase))
            return;
        await BootstrapAsync(slug, ct);
    }

    private async Task EnsureDevice(CancellationToken ct)
    {
        _ = ct;
        if (string.IsNullOrWhiteSpace(_device))
            _device = await _fp.GetAsync();
    }

    private async Task<HttpResponseMessage> DispatchAsync(HttpMethod method, string path, byte[]? body, CancellationToken ct)
    {
        await EnsureDevice(ct);
        var urls = CandidateUrls(path);
        HttpResponseMessage? last = null;
        for (int i = 0; i < urls.Count; i++)
        {
            last?.Dispose();
            using var req = new HttpRequestMessage(method, urls[i]);
            AddHeaders(req);
            if (body is not null)
            {
                req.Content = new ByteArrayContent(body);
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };
            }

            var resp = await _http.SendAsync(req, ct);
            bool tryNext = i < urls.Count - 1 && (
                resp.StatusCode == System.Net.HttpStatusCode.NotFound
                || IsHtml(resp));
            if (!tryNext)
                return resp;
            last = resp;
        }

        return last!;
    }

    private List<string> CandidateUrls(string relative)
    {
        var list = new List<string>(2);
        if (!IsLocalHost() && !string.IsNullOrWhiteSpace(_slug))
            list.Add(EdgeUrl(relative));
        list.Add(Abs(relative));
        return list;
    }

    private string EdgeUrl(string relative)
    {
        string rest = relative.StartsWith("api/webstore/", StringComparison.OrdinalIgnoreCase)
            ? relative["api/webstore/".Length..]
            : relative.TrimStart('/');
        return new Uri(new Uri(_nav.BaseUri), $"api/pos/{_slug}/{rest}").ToString();
    }

    private bool IsLocalHost()
    {
        var host = new Uri(_nav.Uri).Host;
        return host is "localhost" or "127.0.0.1";
    }

    private void AddHeaders(HttpRequestMessage req)
    {
        if (!string.IsNullOrWhiteSpace(_slug))
            req.Headers.TryAddWithoutValidation("X-Mahgoub-Slug", _slug);
        if (!string.IsNullOrWhiteSpace(_device))
            req.Headers.TryAddWithoutValidation("X-Mahgoub-Device", _device);
    }

    private static bool IsHtml(HttpResponseMessage resp)
    {
        var media = resp.Content.Headers.ContentType?.MediaType;
        return media is not null && media.Contains("html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage resp, CancellationToken ct)
    {
        if (IsHtml(resp))
            return default;
        try { return await resp.Content.ReadFromJsonAsync<T>(Json, ct); }
        catch { return default; }
    }
}
