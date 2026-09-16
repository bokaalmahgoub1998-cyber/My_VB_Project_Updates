-- يولّد سطر تعليق يُلصق في Mahgoub_store_data.txt فقط
-- لا تلصقه في Mahgoub_server_name.txt (ذلك الملف لسلسلة اتصال SQL فقط).

SELECT N'# STORES ' + (
    SELECT
        store_slug AS slug,
        NULLIF(LTRIM(RTRIM(api_base_url)), N'') AS tunnel,
        COALESCE(NULLIF(LTRIM(RTRIM(store_display_name)), N''), license_owner_name) AS name,
        store_primary_color AS color,
        CONVERT(char(10), store_expiry_date, 23) AS expiry,
        CAST(ISNULL(store_enabled, 0) AS bit) AS enabled
    FROM dbo.Mahgoub_Supp_d
    WHERE store_slug IS NOT NULL AND LTRIM(RTRIM(store_slug)) <> N''
    FOR JSON PATH, ROOT('stores')
);
