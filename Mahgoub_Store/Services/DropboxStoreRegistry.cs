using System.Text.Json;

namespace Mahgoub_Store.Services;

public sealed class DropboxStoreRecord
{
    public string Slug { get; set; } = "";
    public string? Name { get; set; }
    public string? Color { get; set; }
    public string? Tunnel { get; set; }
    public string? Expiry { get; set; }
    public bool Enabled { get; set; } = true;
}

public static class DropboxStoreRegistry
{
    public static DropboxStoreRecord? Find(string raw, string slug)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrWhiteSpace(slug))
            return null;

        slug = slug.Trim();
        foreach (string line in raw.Replace("\r", "\n").Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("# STORES", StringComparison.OrdinalIgnoreCase))
            {
                int jsonAt = t.IndexOf('{');
                if (jsonAt < 0)
                    continue;
                var one = TryFindJson(t[jsonAt..], slug);
                if (one is not null)
                    return one;
                continue;
            }

            if (t.StartsWith("Store.", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
            {
                int eq = t.IndexOf('=');
                string lineSlug = t[6..eq].Trim();
                if (!string.Equals(lineSlug, slug, StringComparison.OrdinalIgnoreCase))
                    continue;
                string[] parts = t[(eq + 1)..].Split('|');
                return new DropboxStoreRecord
                {
                    Slug = lineSlug,
                    Tunnel = (parts.ElementAtOrDefault(0) ?? "").Trim()
                };
            }
        }

        return null;
    }

    public static List<DropboxStoreRecord> Parse(string? raw)
    {
        var list = new List<DropboxStoreRecord>();
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        foreach (string line in raw.Replace("\r", "\n").Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("# STORES", StringComparison.OrdinalIgnoreCase))
            {
                int jsonAt = t.IndexOf('{');
                if (jsonAt < 0)
                    continue;
                TryAddJson(t[jsonAt..], list);
                continue;
            }

            if (t.StartsWith("Store.", StringComparison.OrdinalIgnoreCase) && t.Contains('='))
            {
                int eq = t.IndexOf('=');
                string slug = t[6..eq].Trim();
                string[] parts = t[(eq + 1)..].Split('|');
                list.Add(new DropboxStoreRecord
                {
                    Slug = slug,
                    Tunnel = (parts.ElementAtOrDefault(0) ?? "").Trim()
                });
            }
        }

        return list;
    }

    private static DropboxStoreRecord? TryFindJson(string json, string slug)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("stores", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var el in arr.EnumerateArray())
            {
                string s = el.TryGetProperty("slug", out var slugEl) ? slugEl.GetString() ?? "" : "";
                if (!string.Equals(s, slug, StringComparison.OrdinalIgnoreCase))
                    continue;
                return new DropboxStoreRecord
                {
                    Slug = s,
                    Tunnel = el.TryGetProperty("tunnel", out var t) ? t.GetString() : null
                };
            }
        }
        catch
        {
            /* JSON غير صالح */
        }

        return null;
    }

    private static void TryAddJson(string json, List<DropboxStoreRecord> list)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("stores", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            foreach (var el in arr.EnumerateArray())
            {
                list.Add(new DropboxStoreRecord
                {
                    Slug = el.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "",
                    Tunnel = el.TryGetProperty("tunnel", out var t) ? t.GetString() : null
                });
            }
        }
        catch
        {
            /* تعليق JSON غير صالح — يُتجاهل */
        }
    }
}
