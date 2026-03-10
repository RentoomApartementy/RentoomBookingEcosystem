namespace RentoomBooking.SharedClasses.Models
{
    public class CustomerTermDisplayDto
    {
        public int Id { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public bool IsRequired { get; set; }
    }
}
