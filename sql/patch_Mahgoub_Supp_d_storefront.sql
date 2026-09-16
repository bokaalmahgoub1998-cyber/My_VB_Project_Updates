USE Mahgoub_bootDb;
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_display_name') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_display_name NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_primary_color') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_primary_color CHAR(7) NULL;
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_logo') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_logo VARBINARY(MAX) NULL;
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_logo_content_type') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_logo_content_type NVARCHAR(80) NULL;
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_expiry_date') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_expiry_date DATE NULL;
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_enabled') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_enabled BIT NOT NULL
        CONSTRAINT DF_Mahgoub_Supp_d_store_enabled DEFAULT (0);
END
GO

IF COL_LENGTH('dbo.Mahgoub_Supp_d', 'store_slug') IS NULL
BEGIN
    ALTER TABLE dbo.Mahgoub_Supp_d
    ADD store_slug NVARCHAR(80) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Mahgoub_Supp_d')
      AND name = 'UX_Mahgoub_Supp_d_store_slug'
)
BEGIN
    CREATE UNIQUE INDEX UX_Mahgoub_Supp_d_store_slug
        ON dbo.Mahgoub_Supp_d(store_slug)
        WHERE store_slug IS NOT NULL;
END
GO
