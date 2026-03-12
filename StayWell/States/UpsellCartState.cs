using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;

public class UpsellCartState
{
    public record UpsellVisualInfo(
        Dictionary<PartnerServiceBannerPlacementType, string> BannerUrls,
        decimal? Discount,
        PartnerServiceDiscountType PricingDiscountType
    );

    public record CartAddonItem(
        int IdoBookingAddonId,
        string Title,
        string Description,
        string Icon,
        string PriceText,
        string PriceInfo,
        decimal? PriceGross = null,
        int Quantity = 1,
        bool AllowQuantitySelection = false,
        int? MaxQuantity = null
    );

    private Dictionary<int, UpsellVisualInfo> _visuals = [];
    private List<SelectedUpsellDto> _pendingUpsells = [];
    private List<UpsellSummaryLineDto> _items = [];
    private List<CartAddonItem> _addonItems = [];

    public IReadOnlyDictionary<int, UpsellVisualInfo> Visuals => _visuals;
    public IReadOnlyList<SelectedUpsellDto> PendingUpsells => _pendingUpsells.AsReadOnly();
    public IReadOnlyList<UpsellSummaryLineDto> Items => _items.AsReadOnly();
    public decimal TotalPrice => _items.Sum(i => i.LineTotalGross);
    public decimal TotalAddonPrice => _addonItems.Sum(a => (a.PriceGross ?? 0m) * a.Quantity);
    public decimal TotalCombinedPrice => TotalPrice + TotalAddonPrice;

    public IReadOnlyList<CartAddonItem> AddonItems => _addonItems.AsReadOnly();

    public string Currency { get; private set; } = "PLN";
    public bool IsEmpty => _items.Count == 0 && _addonItems.Count == 0;
    public int TotalQuantity => _items.Sum(i => i.Quantity) + _addonItems.Sum(a => a.Quantity);
    public int TotalLineCount => _items.Count + _addonItems.Count;

    public event Action? OnChange;
    public event Action? OnCleared;

    public void SetPendingUpsells(List<SelectedUpsellDto> selected)
    {
        _pendingUpsells = [.. selected];
        NotifyStateChanged();
    }

    public void SetVisuals(IEnumerable<UpsellTileDto> tiles)
    {
        _visuals = tiles.ToDictionary(
            t => t.PartnerServiceId,
            t => new UpsellVisualInfo(t.BannerUrls, t.Discount, t.PricingDiscountType)
        );
        NotifyStateChanged();
    }

    public void SetFromSummary(List<UpsellSummaryLineDto> lines, string? currency = null)
    {
        _items = [.. lines];

        _pendingUpsells = lines
            .Select(l => new SelectedUpsellDto
            {
                PartnerServiceId = l.PartnerServiceId,
                Quantity = l.Quantity
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(currency))
            Currency = currency;

        NotifyStateChanged();
    }

    public string? GetBannerUrl(int partnerServiceId, PartnerServiceBannerPlacementType placement)
    {
        if (_visuals.TryGetValue(partnerServiceId, out var info)
            && info.BannerUrls.TryGetValue(placement, out var url))
            return url;
        return null;
    }

    public decimal? GetDiscount(int partnerServiceId) =>
        _visuals.TryGetValue(partnerServiceId, out var info) ? info.Discount : null;

    public List<SelectedUpsellDto> ToSelectedUpsells() =>
        _items
            .Select(i => new SelectedUpsellDto
            {
                PartnerServiceId = i.PartnerServiceId,
                Quantity = i.Quantity
            })
            .ToList();

    public void RemoveItem(int partnerServiceId)
    {
        _items.RemoveAll(i => i.PartnerServiceId == partnerServiceId);
        _pendingUpsells.RemoveAll(p => p.PartnerServiceId == partnerServiceId);
        NotifyStateChanged();
    }

    public bool IsAddonSelected(int idoBookingAddonId) =>
        _addonItems.Any(a => a.IdoBookingAddonId == idoBookingAddonId);

    public int GetAddonQuantity(int idoBookingAddonId) =>
        _addonItems.FirstOrDefault(a => a.IdoBookingAddonId == idoBookingAddonId)?.Quantity ?? 1;

    public void ToggleAddon(CartAddonItem addon)
    {
        var existing = _addonItems.FirstOrDefault(a => a.IdoBookingAddonId == addon.IdoBookingAddonId);
        if (existing is not null)
            _addonItems.Remove(existing);
        else
            _addonItems.Add(addon);
        NotifyStateChanged();
    }

    public void SetAddonQuantity(int idoBookingAddonId, int quantity)
    {
        if (quantity < 1)
            return;

        var index = _addonItems.FindIndex(a => a.IdoBookingAddonId == idoBookingAddonId);
        if (index < 0)
            return;

        _addonItems[index] = _addonItems[index] with { Quantity = quantity };
        NotifyStateChanged();
    }

    public void RemoveAddonItem(int idoBookingAddonId)
    {
        _addonItems.RemoveAll(a => a.IdoBookingAddonId == idoBookingAddonId);
        NotifyStateChanged();
    }

    public void SetCurrency(string currency)
    {
        Currency = currency;
        NotifyStateChanged();
    }

    public void Clear()
    {
        _pendingUpsells.Clear();
        _items.Clear();
        _addonItems.Clear();
        OnCleared?.Invoke();
        NotifyStateChanged();
    }

    public void RefreshItemTitlesFromCatalog(IReadOnlyList<UpsellTileDto> updatedTiles)
    {
        var tileLookup = updatedTiles.ToDictionary(t => t.PartnerServiceId);
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (!tileLookup.TryGetValue(item.PartnerServiceId, out var tile))
                continue;

            _items[i] = new UpsellSummaryLineDto
            {
                PartnerServiceId = item.PartnerServiceId,
                Title = tile.Title,
                PricingModel = item.PricingModel,
                Quantity = item.Quantity,
                UnitPriceGross = item.UnitPriceGross,
                Nights = item.Nights,
                TotalGuests = item.TotalGuests,
                LineTotalGross = item.LineTotalGross,
                DisplayText = tile.ShortDescription ?? item.DisplayText
            };
        }
        NotifyStateChanged();
    }

    public void ReplaceAddonItems(List<CartAddonItem> updatedAddons)
    {
        _addonItems = updatedAddons;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}