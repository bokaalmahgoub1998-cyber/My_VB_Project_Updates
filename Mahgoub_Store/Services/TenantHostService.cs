using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Mahgoub_Store.Services;

public sealed class TenantHostService
{
    private static readonly Regex SlugRx = new(
        @"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "cart", "weather", "counter", "api", "css", "lib", "img", "images",
        "_framework", "_content", "_app", "www", "wwwroot", "store-api",
        "favicon.ico", "index.html"
    };

    private readonly NavigationManager _nav;
    private readonly IConfiguration _cfg;

    public TenantHostService(NavigationManager nav, IConfiguration cfg)
    {
        _nav = nav;
        _cfg = cfg;
    }

    public string? ResolveSlug()
    {
        var uri = new Uri(_nav.Uri);

        if (IsSlug(ReadQuery(uri, "slug"), out var fromQuery))
            return fromQuery;

        if (PathSlug(uri) is { } fromPath)
            return fromPath;

        if (SubdomainSlug(uri.Host) is { } fromHost)
            return fromHost;

        if (IsGenericHost(uri.Host) && IsSlug(_cfg["DevSlug"], out var dev))
            return dev;

        return null;
    }

    public string HomeUrl()
    {
        string? slug = ResolveSlug();
        if (string.IsNullOrEmpty(slug) || !UsesPathPrefix())
            return "/";
        return "/" + slug;
    }

    public string CartUrl()
    {
        string? slug = ResolveSlug();
        if (string.IsNullOrEmpty(slug) || !UsesPathPrefix())
            return "/cart";
        return "/" + slug + "/cart";
    }

    public bool UsesPathPrefix()
    {
        var uri = new Uri(_nav.Uri);
        if (PathSlug(uri) is not null)
            return true;
        return IsGenericHost(uri.Host);
    }

    private static string? PathSlug(Uri uri)
    {
        var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segs.Length == 0)
            return null;
        return IsSlug(segs[0], out var slug) ? slug : null;
    }

    private static string? SubdomainSlug(string host)
    {
        host = host.ToLowerInvariant();
        if (IsGenericHost(host))
        {
            var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (host.EndsWith(".pages.dev", StringComparison.Ordinal) && labels.Length >= 4
                && IsSlug(labels[0], out var nested))
                return nested;
            return null;
        }

        int dot = host.IndexOf('.');
        if (dot <= 0)
            return null;

        return IsSlug(host[..dot], out var label) ? label : null;
    }

    private static bool IsGenericHost(string host)
    {
        host = host.ToLowerInvariant();
        return host is "localhost" or "127.0.0.1"
            or "mahgoubonline.com" or "mahjoub-online.com"
            || host.EndsWith(".pages.dev", StringComparison.Ordinal);
    }

    private static bool IsSlug(string? raw, out string slug)
    {
        slug = (raw ?? "").Trim().ToLowerInvariant();
        if (slug.Length == 0 || Reserved.Contains(slug) || !SlugRx.IsMatch(slug))
        {
            slug = "";
            return false;
        }
        return true;
    }

    private static string? ReadQuery(Uri uri, string key)
    {
        string q = uri.Query.TrimStart('?');
        if (q.Length == 0)
            return null;

        foreach (string part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            if (!string.Equals(Uri.UnescapeDataString(part[..eq]), key, StringComparison.OrdinalIgnoreCase))
                continue;
            return Uri.UnescapeDataString(part[(eq + 1)..]);
        }

        return null;
    }
}
