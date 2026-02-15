namespace RentoomBooking.SharedFrontend.Components.Shared.UpsellComponents;

public static class UpsellTextConfigFactory
{
    public static UpsellTextConfig Create(Func<string, string> getText)
    {
        return new UpsellTextConfig
        {
            Title = getText("ListTitle"),
            SubTitle = getText("ListSubtitle"),
            BadgeInfo = getText("BadgeInfo"),
            NightsText = getText("Nights"),
            GuestsText = getText("Guests"),
            DeleteText = getText("Delete"),
            AddText = getText("Add"),
            DescriptionText = getText("Description"),
            TermsTitleText = getText("TermsTitle"),
            TermsDescriptionText = getText("TermsDescription"),
            PriceLabel = getText("Price"),
            PricingPerPersonPerDayText = getText("PricingPerPersonPerDay"),
            PricingPerApartmentPerDayText = getText("PricingPerApartmentPerDay"),
            PricingPerStayText = getText("PricingPerStay"),
            PricingOneTimeText = getText("PricingOneTime"),
            LinkLabel = getText("LinkLabel"),
            DetailsText = getText("Details"),
            ShowVoucherCodeText = getText("ShowVoucherCode"),
            YourCodeText = getText("YourCode"),
            ManualCodeLabelText = getText("ManualCodeLabel"),
            VoucherQrAltText = getText("VoucherQrAlt"),
            QrUnavailableText = getText("QrUnavailable"),
            EnlargeText = getText("Enlarge"),
            AddressText = getText("Address"),
            VoucherUsageTitleText = getText("VoucherUsageTitle"),
            VoucherStatusText = getText("VoucherStatus"),
            VoucherUsedCountText = getText("VoucherUsedCount"),
            VoucherRemainingText = getText("VoucherRemaining"),
            VoucherValidFromText = getText("VoucherValidFrom"),
            VoucherValidToText = getText("VoucherValidTo"),
            VoucherNoLimitText = getText("VoucherNoLimit"),
            ShowInRestaurantText = getText("ShowInRestaurant"),
            VoucherCodeText = getText("VoucherCode"),
            VoucherOverlayHintText = getText("VoucherOverlayHint")
        };
    }
}
