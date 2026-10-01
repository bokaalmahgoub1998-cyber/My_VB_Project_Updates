using System.Text.Json.Serialization;

namespace Mahgoub_Store.Models;

public sealed class StoreBootstrapDto
{
    public bool Available { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string PrimaryColor { get; set; } = "#254055";
    public bool HasLogo { get; set; }
    public string? LogoUrl { get; set; }
    public string? TunnelUrl { get; set; }
    public string? Message { get; set; }
    public int DiscountPercent { get; set; }
    public string MapText { get; set; } = "";
    public bool Restaurant { get; set; }
}

public sealed class StoreCatalogDto
{
    public List<StoreCategoryDto> Categories { get; set; } = [];
    public List<StoreItemDto> Items { get; set; } = [];
    public int DiscountPercent { get; set; }
}

public sealed class StoreCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class StoreItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public decimal Price { get; set; }
    public decimal SalePrice { get; set; }
    public int DiscountPercent { get; set; }
    public string Unit { get; set; } = "قطعة";
    public bool HasImage { get; set; }
    public decimal EffectivePrice
    {
        get
        {
            if (DiscountPercent <= 0)
                return Price;
            if (SalePrice > 0 || DiscountPercent >= 100)
                return SalePrice;
            return Math.Round(Price * (100 - DiscountPercent) / 100m, 3, MidpointRounding.AwayFromZero);
        }
    }
}

public sealed class StorePaymentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int FeePercent { get; set; }
}

public sealed class CartLine
{
    public int ItemId { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public int Qty { get; set; } = 1;
    public string Note { get; set; } = "";
    public bool Unavailable { get; set; }
}

public sealed class SubmitOrderRequest
{
    public string CustomerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public int Fulfillment { get; set; }
    public string? Address { get; set; }
    public string? OrderNote { get; set; }
    public string? ClientFingerprint { get; set; }
    public int PaymentId { get; set; }
    public List<SubmitOrderLine> Lines { get; set; } = [];
}

public sealed class SubmitOrderLine
{
    public int ItemId { get; set; }
    public int Qty { get; set; }
    public string? Note { get; set; }
}

public sealed class SubmitOrderResult
{
    public Guid OrderId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string? Message { get; set; }
    public List<int> UnavailableItemIds { get; set; } = [];
}

public sealed class OrderStatusDto
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int? InvoiceNo { get; set; }
}

public sealed class LockedOrderState
{
    public Guid OrderId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public string Phone { get; set; } = "";
}
