namespace RentoomBooking.SharedClasses.Models.AvailableTerms;

public class ApartmentAvailableTermsResult
{
    public int ApartmentId { get; set; }
    public List<AvailableTerm> AvailableTerms { get; set; } = new();
}
