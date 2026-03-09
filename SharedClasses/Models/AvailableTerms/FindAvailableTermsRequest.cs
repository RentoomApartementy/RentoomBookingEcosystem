namespace RentoomBooking.SharedClasses.Models.AvailableTerms;

public class FindAvailableTermsRequest
{
    public List<int>? ApartmentIds { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public int Adults { get; set; } = 2;
    public int Children { get; set; }
}
