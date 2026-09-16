namespace Mahgoub_Store_Api.Services;

public sealed class StoreTenantRecord
{
    public required string ProductKey { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public required string PrimaryColor { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public bool StoreEnabled { get; init; }
    public bool InService { get; init; }
    public string? TunnelUrl { get; init; }
    public bool HasLogo { get; init; }
}

public sealed class StoreTenantService
{
    private readonly CompanyDatabaseConnectionProvider _db;
    private readonly ILogger<StoreTenantService> _log;

    public StoreTenantService(CompanyDatabaseConnectionProvider db, ILogger<StoreTenantService> log)
    {
        _db = db;
        _log = log;
    }

    public static string? NormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        string s = raw.Trim().ToLowerInvariant();
        if (s.Length < 2 || s.Length > 80)
            return null;

        foreach (char c in s)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '-'))
                return null;
        }

        return s.Trim('-');
    }

    public async Task<StoreTenantRecord?> TryGetBySlugAsync(string slug, CancellationToken ct)
    {
        string? n = NormalizeSlug(slug);
        if (n is null)
            return null;

        const string sql = """
            SELECT TOP (1)
                d_name,
                store_slug,
                COALESCE(NULLIF(LTRIM(RTRIM(store_display_name)), N''), NULLIF(LTRIM(RTRIM(license_owner_name)), N''), N'متجر') AS display_name,
                NULLIF(LTRIM(RTRIM(store_primary_color)), N'') AS color,
                store_expiry_date,
                CAST(ISNULL(store_enabled, 0) AS bit) AS store_enabled,
                CAST(COALESCE(is_in_service, CAST(1 AS bit)) AS bit) AS in_service,
                NULLIF(LTRIM(RTRIM(api_base_url)), N'') AS tunnel,
                CASE WHEN store_logo IS NULL THEN 0 ELSE 1 END AS has_logo
            FROM dbo.Mahgoub_Supp_d
            WHERE store_slug = @slug
            """;

        try
        {
            string cs = await _db.GetConnectionStringAsync(ct).ConfigureAwait(false);
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@slug", n);
            await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await rd.ReadAsync(ct).ConfigureAwait(false))
                return null;

            string color = rd.IsDBNull(3) ? "#254055" : rd.GetString(3).Trim();
            if (color.Length != 7 || color[0] != '#')
                color = "#254055";

            return new StoreTenantRecord
            {
                ProductKey = rd.GetString(0).Trim(),
                Slug = rd.IsDBNull(1) ? n : rd.GetString(1).Trim(),
                DisplayName = rd.IsDBNull(2) ? "متجر" : rd.GetString(2).Trim(),
                PrimaryColor = color,
                ExpiryDate = rd.IsDBNull(4) ? null : rd.GetDateTime(4).Date,
                StoreEnabled = !rd.IsDBNull(5) && rd.GetBoolean(5),
                InService = rd.IsDBNull(6) || rd.GetBoolean(6),
                TunnelUrl = rd.IsDBNull(7) ? null : rd.GetString(7).Trim().TrimEnd('/'),
                HasLogo = !rd.IsDBNull(8) && Convert.ToInt32(rd.GetValue(8)) == 1
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "فشل قراءة متجر {Slug}", n);
            return null;
        }
    }

    public async Task<(byte[] Bytes, string ContentType)?> TryGetLogoAsync(string slug, CancellationToken ct)
    {
        string? n = NormalizeSlug(slug);
        if (n is null)
            return null;

        const string sql = """
            SELECT TOP (1) store_logo, NULLIF(LTRIM(RTRIM(store_logo_content_type)), N'')
            FROM dbo.Mahgoub_Supp_d
            WHERE store_slug = @slug AND store_logo IS NOT NULL
            """;

        try
        {
            string cs = await _db.GetConnectionStringAsync(ct).ConfigureAwait(false);
            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(cs);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@slug", n);
            await using var rd = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await rd.ReadAsync(ct).ConfigureAwait(false) || rd.IsDBNull(0))
                return null;

            byte[] bytes = (byte[])rd.GetValue(0);
            string type = rd.IsDBNull(1) ? "image/webp" : rd.GetString(1).Trim();
            if (string.IsNullOrWhiteSpace(type))
                type = "image/webp";
            return (bytes, type);
        }
        catch
        {
            return null;
        }
    }

    public static string? PublicUnavailableReason(StoreTenantRecord t)
    {
        if (!t.StoreEnabled || !t.InService)
            return "هذا المتجر غير متاح حالياً، يرجى الزيارة لاحقاً";

        if (t.ExpiryDate is DateTime exp && exp.Date < DateTime.Today)
            return "هذا المتجر غير متاح حالياً، يرجى الزيارة لاحقاً";

        if (string.IsNullOrWhiteSpace(t.TunnelUrl))
            return "المتجر غير متاح حالياً، يرجى الزيارة لاحقاً";

        return null;
    }
}
