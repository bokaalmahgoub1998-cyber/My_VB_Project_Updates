using Microsoft.Data.SqlClient;

namespace Mahgoub_Store_Api.Services;

/// <summary>
/// سلسلة اتصال قاعدة الشركة — نفس آلية MauiApp2: ملف Dropbox ثم احتياطي مضمّن.
/// لا تُعرَّض للمتصفح.
/// </summary>
public sealed class CompanyDatabaseConnectionProvider
{
    public const string DefaultDropboxConfigUrl =
        "https://www.dropbox.com/scl/fi/xweqmc3g0qoirem2p97tq/Mahgoub_server_name.txt?rlkey=gi419f9r9ecyou49ux1jcajq4&dl=1";

    public const string DefaultConnectionString =
        "Server=ws05.server.ly;Database=Mahgoub_bootDb;" +
        "User Id=Mahgoub_bootDbUser;Password=#Pt3Uz9mi!Fco5gm;" +
        "Connect Timeout=10;TrustServerCertificate=True;";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _cached = DefaultConnectionString;
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);

    public string CurrentConnectionString => _cached;

    public async Task<string> GetConnectionStringAsync(CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _lastRefreshUtc < CacheTtl)
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
            _cached = string.IsNullOrWhiteSpace(remote) ? DefaultConnectionString : remote;
            _lastRefreshUtc = DateTime.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string?> TryFetchAndParseAsync(CancellationToken ct)
    {
        string url = Environment.GetEnvironmentVariable("MAHGOUB_COMPANY_DB_CONFIG_URL") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            url = DefaultDropboxConfigUrl;

        url = NormalizeDropboxDirectUrl(url.Trim());

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
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

            ApplyKeyValue(sb, t[..eq].Trim(), t[(eq + 1)..].Trim().Trim('"'));
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
        catch { /* مفتاح غير معروف */ }
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
