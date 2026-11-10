using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Mahgoub_Store_Api.Services;

/// <summary>
/// سلسلة اتصال قاعدة الشركة — نفس عقلية MauiApp2 / المتجر:
/// بوابة Cloudflare <c>/api/company-config</c> بتوقيع Gate+HMAC،
/// ورابط Dropbox يبقى Secret على Cloudflare فقط.
/// </summary>
public sealed class CompanyDatabaseConnectionProvider
{
    public const string DefaultCompanyConfigGatewayUrl = "https://mahgoubonline.com/api/company-config";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _cached = "";
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    public string CurrentConnectionString => _cached;

    public async Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cached) && DateTime.UtcNow - _lastRefreshUtc < CacheTtl)
            return _cached;

        await RefreshAsync(ct).ConfigureAwait(false);
        return _cached;
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string? remote = await TryFetchAndParseAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(remote))
                _cached = remote;
            _lastRefreshUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string?> TryFetchAndParseAsync(CancellationToken ct)
    {
        string url = (Environment.GetEnvironmentVariable("MAHGOUB_COMPANY_DB_CONFIG_URL") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url))
            url = DefaultCompanyConfigGatewayUrl;

        string hmac = (Environment.GetEnvironmentVariable("MAHGOUB_COMPANY_CONFIG_HMAC")
                       ?? Environment.GetEnvironmentVariable("COMPANY_CONFIG_HMAC_SECRET")
                       ?? "").Trim();
        string gate = (Environment.GetEnvironmentVariable("MAHGOUB_COMPANY_CONFIG_GATE")
                       ?? Environment.GetEnvironmentVariable("COMPANY_CONFIG_GATE_TOKEN")
                       ?? "").Trim();

        bool viaGateway = IsCompanyConfigGatewayUrl(url);
        if (!viaGateway)
            url = NormalizeDropboxDirectUrl(url);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (viaGateway)
            {
                if (hmac.Length < 16 || gate.Length < 16)
                    return null;

                var uri = new Uri(url);
                string pathQ = uri.PathAndQuery;
                string ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                string sign = ComputeHmac(hmac, "GET", pathQ, ts, nonce, Array.Empty<byte>());
                req.Headers.TryAddWithoutValidation("X-Mahgoub-Gate", gate);
                req.Headers.TryAddWithoutValidation("X-Mahgoub-Ts", ts);
                req.Headers.TryAddWithoutValidation("X-Mahgoub-Nonce", nonce);
                req.Headers.TryAddWithoutValidation("X-Mahgoub-Sign", sign);
            }

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            string text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseConfigText(text);
        }
        catch
        {
            return null;
        }
    }

    internal static bool IsCompanyConfigGatewayUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u))
            return false;
        string path = (u.AbsolutePath ?? "").TrimEnd('/');
        return path.Equals("/api/company-config", StringComparison.OrdinalIgnoreCase);
    }

    internal static string NormalizeDropboxDirectUrl(string url)
    {
        if (!url.Contains("dropbox.com", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.Contains("dl=0", StringComparison.OrdinalIgnoreCase))
            return url.Replace("dl=0", "dl=1", StringComparison.OrdinalIgnoreCase);

        if (!url.Contains("dl=1", StringComparison.OrdinalIgnoreCase))
            url += url.Contains('?') ? "&dl=1" : "?dl=1";

        return url;
    }

    internal static string ComputeHmac(string secret, string method, string pathAndQuery, string unixSeconds, string nonce, byte[] body)
    {
        string bodyHash = Convert.ToHexString(SHA256.HashData(body ?? [])).ToLowerInvariant();
        string msg = $"{unixSeconds}\n{nonce}\n{method.Trim().ToUpperInvariant()}\n{pathAndQuery}\n{bodyHash}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes((secret ?? "").Trim()));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(msg))).ToLowerInvariant();
    }

    internal static string? ParseConfigText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string compact = raw.Replace("\r", "\n").Trim();
        string oneLine = compact.Replace('\n', ' ').Trim();
        while (oneLine.Contains("  ", StringComparison.Ordinal))
            oneLine = oneLine.Replace("  ", " ", StringComparison.Ordinal);

        if (oneLine.Contains("Server=", StringComparison.OrdinalIgnoreCase) &&
            oneLine.Contains(';'))
            return NormalizeConnectionString(oneLine);

        var sb = new SqlConnectionStringBuilder();
        foreach (string line in compact.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#') || t.StartsWith("//", StringComparison.Ordinal))
                continue;

            int eq = t.IndexOf('=');
            if (eq <= 0)
                continue;

            string key = t[..eq].Trim();
            string val = t[(eq + 1)..].Trim().Trim('"');
            ApplyKeyValue(sb, key, val);
        }

        if (string.IsNullOrWhiteSpace(sb.DataSource) || string.IsNullOrWhiteSpace(sb.InitialCatalog))
            return null;

        return NormalizeConnectionString(sb.ConnectionString);
    }

    private static void ApplyKeyValue(SqlConnectionStringBuilder sb, string key, string val)
    {
        if (string.Equals(key, "Server", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Data Source", StringComparison.OrdinalIgnoreCase))
        {
            sb.DataSource = val;
            return;
        }

        if (string.Equals(key, "Database", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Initial Catalog", StringComparison.OrdinalIgnoreCase))
        {
            sb.InitialCatalog = val;
            return;
        }

        try { sb[key] = val; }
        catch { /* ignore */ }
    }

    private static string NormalizeConnectionString(string cs)
    {
        var sb = new SqlConnectionStringBuilder(cs.Trim());
        if (!sb.ContainsKey("TrustServerCertificate"))
            sb.TrustServerCertificate = true;
        if (sb.ConnectTimeout < 6)
            sb.ConnectTimeout = 10;
        return sb.ConnectionString;
    }
}
