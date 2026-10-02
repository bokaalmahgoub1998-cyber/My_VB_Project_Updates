using Mahgoub_Store.Models;
using Microsoft.JSInterop;

namespace Mahgoub_Store.Services;

public sealed class CartService
{
    public event Action? Changed;
    private readonly List<CartLine> _lines = [];
    public IReadOnlyList<CartLine> Lines => _lines;
    public int TotalQty => _lines.Sum(x => x.Qty);
    public decimal Subtotal => _lines.Sum(x => x.Price * x.Qty);
    public decimal OriginalSubtotal => _lines.Sum(x => (x.OriginalPrice > 0 ? x.OriginalPrice : x.Price) * x.Qty);
    public bool HasDiscount => _lines.Any(x => x.DiscountPercent > 0 && x.OriginalPrice > x.Price);

    public void Add(StoreItemDto item, int qty = 1, string? note = null)
    {
        qty = Math.Clamp(qty, 1, 99);
        string n = (note ?? "").Trim();
        if (n.Length > 200)
            n = n[..200];

        var existing = _lines.FirstOrDefault(x => x.ItemId == item.Id && string.Equals(x.Note, n, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Qty = Math.Clamp(existing.Qty + qty, 1, 99);
            existing.Unavailable = false;
        }
        else
        {
            _lines.Add(new CartLine
            {
                ItemId = item.Id,
                Name = item.Name,
                Price = item.EffectivePrice,
                OriginalPrice = item.Price,
                DiscountPercent = item.DiscountPercent,
                Qty = qty,
                Note = n
            });
        }

        Changed?.Invoke();
    }

    public void SetQty(int itemId, string note, int qty)
    {
        var line = _lines.FirstOrDefault(x => x.ItemId == itemId && x.Note == note);
        if (line is null)
            return;
        if (qty <= 0)
            _lines.Remove(line);
        else
            line.Qty = Math.Clamp(qty, 1, 99);
        Changed?.Invoke();
    }

    public void SetNote(CartLine line, string note)
    {
        line.Note = (note ?? "").Trim();
        if (line.Note.Length > 200)
            line.Note = line.Note[..200];
        Changed?.Invoke();
    }

    public void MarkUnavailable(IEnumerable<int> itemIds)
    {
        var set = itemIds.ToHashSet();
        foreach (var line in _lines)
            line.Unavailable = set.Contains(line.ItemId);
        Changed?.Invoke();
    }

    public void ClearUnavailable()
    {
        foreach (var line in _lines)
            line.Unavailable = false;
        Changed?.Invoke();
    }

    public void Remove(CartLine line)
    {
        _lines.Remove(line);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _lines.Clear();
        Changed?.Invoke();
    }
}

public sealed class OrderLockService
{
    private readonly IJSRuntime _js;

    public OrderLockService(IJSRuntime js) => _js = js;

    private static string Key(string slug) => $"mahgoub.store.lock.{slug}";

    public async Task SaveAsync(string slug, LockedOrderState state)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(state);
        await _js.InvokeVoidAsync("localStorage.setItem", Key(slug), json);
    }

    public async Task<LockedOrderState?> LoadAsync(string slug)
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("localStorage.getItem", Key(slug));
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return System.Text.Json.JsonSerializer.Deserialize<LockedOrderState>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task ClearAsync(string slug) =>
        await _js.InvokeVoidAsync("localStorage.removeItem", Key(slug));
}

public sealed class StoreRunGate
{
    public event Action? Changed;
    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
            return;
        IsRunning = true;
        Changed?.Invoke();
    }

    public void Stop()
    {
        if (!IsRunning)
            return;
        IsRunning = false;
        Changed?.Invoke();
    }
}

public sealed class MenuCacheService
{
    private readonly IJSRuntime _js;
    private static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MenuCacheService(IJSRuntime js) => _js = js;

    public StoreCatalogDto? Catalog { get; private set; }
    public bool HasItems => Catalog?.Items.Count > 0;
    public bool IsComplete => Catalog is not null && Catalog.Total > 0 && Catalog.Items.Count >= Catalog.Total;

    private static string Key(string slug) => "mahgoub.catalog." + slug;

    public async Task RestoreAsync(string slug)
    {
        if (HasItems || string.IsNullOrWhiteSpace(slug))
            return;
        try
        {
            string? json = await _js.InvokeAsync<string?>("sessionStorage.getItem", Key(slug));
            if (string.IsNullOrWhiteSpace(json))
                return;
            Catalog = System.Text.Json.JsonSerializer.Deserialize<StoreCatalogDto>(json, Json);
        }
        catch
        {
            Catalog = null;
        }
    }

    public async Task PersistAsync(string slug)
    {
        if (Catalog is null || string.IsNullOrWhiteSpace(slug))
            return;
        try
        {
            await _js.InvokeVoidAsync("sessionStorage.setItem", Key(slug), System.Text.Json.JsonSerializer.Serialize(Catalog, Json));
        }
        catch
        {
            /* الجلسة ممتلئة — الذاكرة تبقى */
        }
    }

    public void Merge(StoreCatalogDto page)
    {
        if (Catalog is null || Catalog.Items.Count == 0)
        {
            Catalog = page;
            return;
        }

        if (page.Categories.Count > 0)
            Catalog.Categories = page.Categories;
        Catalog.Total = page.Total;
        Catalog.DiscountPercent = page.DiscountPercent;
        var ids = Catalog.Items.Select(i => i.Id).ToHashSet();
        foreach (var item in page.Items)
        {
            if (ids.Add(item.Id))
                Catalog.Items.Add(item);
        }
    }
}
