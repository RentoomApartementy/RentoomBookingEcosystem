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

    private Dictionary<int, UpsellVisualInfo> _visuals = [];
    private List<SelectedUpsellDto> _pendingUpsells = [];  
    private List<UpsellSummaryLineDto> _items = [];

    public IReadOnlyDictionary<int, UpsellVisualInfo> Visuals => _visuals;
    public IReadOnlyList<SelectedUpsellDto> PendingUpsells => _pendingUpsells.AsReadOnly();  
    public IReadOnlyList<UpsellSummaryLineDto> Items => _items.AsReadOnly();
    public bool IsEmpty => _items.Count == 0;
    public int TotalQuantity => _items.Sum(i => i.Quantity);
    public decimal TotalPrice => _items.Sum(i => i.LineTotalGross);
    public string Currency { get; private set; } = "PLN";

    public event Action? OnChange;


    public void SetPendingUpsells(List<SelectedUpsellDto> selected)  
    {
        _pendingUpsells = [.. selected];
        NotifyStateChanged();
    }

    public string? GetBannerUrl(int partnerServiceId, PartnerServiceBannerPlacementType placement)
    {
        if (_visuals.TryGetValue(partnerServiceId, out var info)
            && info.BannerUrls.TryGetValue(placement, out var url))
            return url;
        return null;
    }

    public void SetVisuals(IEnumerable<UpsellTileDto> tiles)
    {
        _visuals = tiles.ToDictionary(
            t => t.PartnerServiceId,
            t => new UpsellVisualInfo(t.BannerUrls, t.Discount, t.PricingDiscountType)
        );
        NotifyStateChanged();
    }

    public decimal? GetDiscount(int partnerServiceId) =>
    _visuals.TryGetValue(partnerServiceId, out var info) ? info.Discount : null;

    public void SetFromSummary(List<UpsellSummaryLineDto> lines, string? currency = null)
    {
        _items = [.. lines];
        if (!string.IsNullOrWhiteSpace(currency))
            Currency = currency;
        NotifyStateChanged();
    }

    public List<SelectedUpsellDto> ToSelectedUpsells() =>
        _items
            .Select(i => new SelectedUpsellDto
            {
                PartnerServiceId = i.PartnerServiceId,
                Quantity = i.Quantity
            })
            .ToList();

    public void SetCurrency(string currency)
    {
        Currency = currency;
        NotifyStateChanged();
    }

    public void RemoveItem(int partnerServiceId)
    {
        _items.RemoveAll(i => i.PartnerServiceId == partnerServiceId);
        _pendingUpsells.RemoveAll(p => p.PartnerServiceId == partnerServiceId);
        NotifyStateChanged();
    }

    public void Clear()
    {
        _pendingUpsells.Clear();
        _items.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}