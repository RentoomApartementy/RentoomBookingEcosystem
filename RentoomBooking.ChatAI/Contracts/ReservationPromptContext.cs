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
}
