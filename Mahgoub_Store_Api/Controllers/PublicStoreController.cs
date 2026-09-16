using Mahgoub_Store_Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Mahgoub_Store_Api.Controllers;

[ApiController]
[Route("api/public/stores/{slug}")]
public sealed class PublicStoreController : ControllerBase
{
    private readonly StoreTenantService _tenants;
    private readonly PosSignedClient _pos;

    public PublicStoreController(StoreTenantService tenants, PosSignedClient pos)
    {
        _tenants = tenants;
        _pos = pos;
    }

    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap(string slug, CancellationToken ct)
    {
        var tenant = await _tenants.TryGetBySlugAsync(slug, ct);
        if (tenant is null)
            return NotFound(new { message = "هذا المتجر غير متاح حالياً، يرجى الزيارة لاحقاً" });

        string? closed = StoreTenantService.PublicUnavailableReason(tenant);
        if (closed is not null)
            return StatusCode(403, new { message = closed, available = false });

        bool ping = await _pos.PingAsync(tenant.TunnelUrl!, tenant.ProductKey, ct);
        if (!ping)
            return StatusCode(503, new { message = "المتجر غير متاح حالياً، يرجى الزيارة لاحقاً", available = false });

        return Ok(new
        {
            available = true,
            slug = tenant.Slug,
            name = tenant.DisplayName,
            primaryColor = tenant.PrimaryColor,
            hasLogo = tenant.HasLogo,
            logoUrl = tenant.HasLogo ? $"/api/public/stores/{tenant.Slug}/logo" : null
        });
    }

    [HttpGet("logo")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Logo(string slug, CancellationToken ct)
    {
        var logo = await _tenants.TryGetLogoAsync(slug, ct);
        if (logo is null)
            return NotFound();

        return File(logo.Value.Bytes, logo.Value.ContentType);
    }

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog(string slug, CancellationToken ct)
    {
        return await ProxyJson(slug, HttpMethod.Get, "/api/webstore/catalog", null, ct);
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(string slug, CancellationToken ct)
    {
        return await ProxyJson(slug, HttpMethod.Get, "/api/webstore/payments", null, ct);
    }

    [HttpGet("images/{itemNo:int}")]
    [ResponseCache(Duration = 120)]
    public async Task<IActionResult> Image(string slug, int itemNo, CancellationToken ct)
    {
        var ready = await TryReadyTenant(slug, ct);
        if (ready.Result is not null)
            return ready.Result;

        var tenant = ready.Tenant!;
        using var resp = await _pos.SendAsync(
            tenant.TunnelUrl!, tenant.ProductKey, HttpMethod.Get,
            $"/api/webstore/images/{itemNo}", null, "image/webp", ct);

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode);

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        var type = resp.Content.Headers.ContentType?.MediaType ?? "image/webp";
        return File(bytes, type);
    }

    [HttpPost("orders")]
    public async Task<IActionResult> Submit(string slug, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        return await ProxyJson(slug, HttpMethod.Post, "/api/webstore/orders", ms.ToArray(), ct);
    }

    [HttpGet("orders/{orderId:guid}")]
    public async Task<IActionResult> Status(string slug, Guid orderId, [FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 80)
            return BadRequest();

        string q = $"/api/webstore/orders/{orderId:D}?token={Uri.EscapeDataString(token.Trim())}";
        return await ProxyJson(slug, HttpMethod.Get, q, null, ct);
    }

    private async Task<(StoreTenantRecord? Tenant, IActionResult? Result)> TryReadyTenant(string slug, CancellationToken ct)
    {
        var tenant = await _tenants.TryGetBySlugAsync(slug, ct);
        if (tenant is null)
            return (null, NotFound(new { message = "هذا المتجر غير متاح حالياً، يرجى الزيارة لاحقاً" }));

        string? closed = StoreTenantService.PublicUnavailableReason(tenant);
        if (closed is not null)
            return (null, StatusCode(403, new { message = closed, available = false }));

        return (tenant, null);
    }

    private async Task<IActionResult> ProxyJson(string slug, HttpMethod method, string path, byte[]? body, CancellationToken ct)
    {
        var ready = await TryReadyTenant(slug, ct);
        if (ready.Result is not null)
            return ready.Result;

        var tenant = ready.Tenant!;
        try
        {
            using var resp = await _pos.SendAsync(tenant.TunnelUrl!, tenant.ProductKey, method, path, body, "application/json", ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(text))
                return StatusCode((int)resp.StatusCode);

            return new ContentResult
            {
                StatusCode = (int)resp.StatusCode,
                Content = text,
                ContentType = "application/json; charset=utf-8"
            };
        }
        catch
        {
            return StatusCode(503, new { message = "المتجر غير متاح حالياً، يرجى الزيارة لاحقاً" });
        }
    }
}
