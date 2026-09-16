using System.Net.Http.Json;
using Mahgoub_Store.Models;
using Microsoft.AspNetCore.Components;

namespace Mahgoub_Store.Services;

public sealed class CentralRegistryService
{
    public const string ClosedMessage = "هذا المتجر غير متاح حالياً، يرجى الزيارة لاحقاً";
    public const string OfflineMessage = "المتجر غير متاح حالياً، يرجى الزيارة لاحقاً";

    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly NavigationManager _nav;

    public CentralRegistryService(HttpClient http, IConfiguration cfg, NavigationManager nav)
    {
        _http = http;
        _cfg = cfg;
        _nav = nav;
    }

    public async Task<StoreBootstrapDto> ResolveAsync(string slug, CancellationToken ct = default)
    {
        slug = slug.Trim().ToLowerInvariant();
        DropboxStoreRecord? fromFile = null;
        try
        {
            fromFile = await TryFromPagesAsync(slug, ct);
        }
        catch
        {
            fromFile = null;
        }

        if (fromFile is null && !IsLocalHost())
            return Offline(slug);

        string? tunnel = fromFile?.Tunnel;
        if (IsLocalHost() && !string.IsNullOrWhiteSpace(_cfg["DevTunnelUrl"]))
            tunnel = _cfg["DevTunnelUrl"];
        if (string.IsNullOrWhiteSpace(tunnel))
            tunnel = fromFile?.Tunnel;
        if (string.IsNullOrWhiteSpace(tunnel))
            return Offline(slug);

        tunnel = tunnel.Trim().TrimEnd('/');
        if (!tunnel.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !tunnel.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return Offline(slug);

        return new StoreBootstrapDto
        {
            Available = true,
            Slug = slug,
            Name = "",
            PrimaryColor = StoreBrandCss.MahgoubNavy,
            TunnelUrl = tunnel
        };
    }

    private async Task<DropboxStoreRecord?> TryFromPagesAsync(string slug, CancellationToken ct)
    {
        using var pages = await _http.GetAsync(new Uri(new Uri(_nav.BaseUri), $"api/tenant/{slug}"), ct);
        if (!pages.IsSuccessStatusCode)
            return null;
        var media = pages.Content.Headers.ContentType?.MediaType;
        if (media is not null && media.Contains("html", StringComparison.OrdinalIgnoreCase))
            return null;

        try { return await pages.Content.ReadFromJsonAsync<DropboxStoreRecord>(ct); }
        catch { return null; }
    }

    private bool IsLocalHost()
    {
        var host = new Uri(_nav.Uri).Host;
        return host is "localhost" or "127.0.0.1";
    }

    private static StoreBootstrapDto Closed(string slug) =>
        new() { Available = false, Slug = slug, Message = ClosedMessage };

    private static StoreBootstrapDto Offline(string slug) =>
        new() { Available = false, Slug = slug, Message = OfflineMessage };
}
