namespace Mahgoub_Store.Helpers;

public static class StoreMapLink
{
    private static readonly HashSet<string> AllowedMapHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "maps.google.com",
        "www.google.com",
        "google.com",
        "maps.app.goo.gl",
        "goo.gl",
        "www.google.ly",
        "maps.google.ly"
    };

    public static string? OpenUrl(string? raw)
    {
        string t = (raw ?? "").Trim();
        if (t.Length == 0)
            return null;
        if (t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return IsAllowedMapUrl(t) ? t : null;
        return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(t);
    }

    public static bool IsAllowedMapUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            return false;
        string host = uri.Host;
        if (AllowedMapHosts.Contains(host))
            return true;
        // maps.google.com/?q=lat,lng style used by device.js
        if (host.Equals("maps.google.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public static string Label(string? raw, bool restaurant)
    {
        string t = (raw ?? "").Trim();
        if (t.Length == 0)
            return "";
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return restaurant ? "موقع المطعم على خرائط جوجل" : "موقع المتجر على خرائط جوجل";
        return t;
    }
}
