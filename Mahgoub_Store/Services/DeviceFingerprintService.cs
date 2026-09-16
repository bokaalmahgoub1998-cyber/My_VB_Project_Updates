using System.Security.Cryptography;
using Microsoft.JSInterop;

namespace Mahgoub_Store.Services;

public sealed class DeviceFingerprintService
{
    private const string Key = "mahgoub.store.device";
    private readonly IJSRuntime _js;
    private string _cached = "";

    public DeviceFingerprintService(IJSRuntime js) => _js = js;

    public async Task<string> GetAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cached))
            return _cached;
        try
        {
            string? existing = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (IsHex(existing))
            {
                _cached = existing!;
                return _cached;
            }
        }
        catch { /* وضع خاص بدون تخزين */ }

        string next = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        try { await _js.InvokeVoidAsync("localStorage.setItem", Key, next); }
        catch { /* تجاهل */ }
        _cached = next;
        return next;
    }

    private static bool IsHex(string? s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length is < 16 or > 64)
            return false;
        foreach (char c in s)
        {
            if (c is < '0' or > '9' && c is < 'a' or > 'f' && c is < 'A' or > 'F')
                return false;
        }
        return true;
    }
}
