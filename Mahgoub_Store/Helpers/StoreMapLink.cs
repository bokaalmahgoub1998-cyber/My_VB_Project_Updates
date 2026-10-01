namespace Mahgoub_Store.Helpers;

public static class StoreMapLink
{
    public static string? OpenUrl(string? raw)
    {
        string t = (raw ?? "").Trim();
        if (t.Length == 0)
            return null;
        if (t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            return t;
        return "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(t);
    }

    public static string Label(string? raw)
    {
        string t = (raw ?? "").Trim();
        if (t.Length == 0)
            return "";
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return "موقع المتجر على خرائط جوجل";
        return t;
    }
}
