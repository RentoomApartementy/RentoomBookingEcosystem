namespace RentoomBooking.ChatAI.Contracts;

public sealed class ReservationPromptContext
{
    public string ReservationToken { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? ApartmentName { get; set; }
    public string? ApartmentAddress { get; set; }
    public string? CheckInDate { get; set; }
    public string? CheckOutDate { get; set; }
    public string? ReservationStatus { get; set; }
    public string? WifiSsid { get; set; }
    public string? WifiPassword { get; set; }
    public string? ArrivalInstructionsSummary { get; set; }
    public string? RulesSummary { get; set; }
    public string? Locale { get; set; }
    public string? ApartmentCity { get; set; }
    public string? ApartmentRegion { get; set; }
    public string? ApartmentCountry { get; set; }
    public double? ApartmentGeoLatitude { get; set; }
    public double? ApartmentGeoLongitude { get; set; }
    public string? ApartmentGoogleMapsUrl { get; set; }
    public string? ApartmentDirectionsSummary { get; set; }
    public string? ReceptionInfo { get; set; }
    public string? ApartmentLocationSummary { get; set; }
    public string? ParkingSpotNumber { get; set; }
    public string? ParkingMapUrl { get; set; }
    public string? ParkingInfoSummary { get; set; }
    public string? GateCode { get; set; }
    public string? BuildingCode { get; set; }
    public string? AdditionalDoorCode { get; set; }
    public string? StoreroomCode { get; set; }
    public string? ApartmentNumberOrItemCode { get; set; }
    public bool? RemoteOpenSupported { get; set; }
    public string? AccessMethodSummary { get; set; }
    public string? NearbyAnswerGuidance { get; set; }
}
