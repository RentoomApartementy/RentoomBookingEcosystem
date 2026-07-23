namespace RentoomBookingWeb.Components.Features.Apartments;

/// <summary>
/// Controls how an apartments listing uses the IdoBooking public offer price.
/// </summary>
public enum ShowPublicOfferTypes
{
    /// <summary>Always show the public offer price; do not consider the dated (term) offer.</summary>
    Enforce,

    /// <summary>Show the public offer price only when there is no dated offer for the selected term. Default.</summary>
    AsFallback
}
