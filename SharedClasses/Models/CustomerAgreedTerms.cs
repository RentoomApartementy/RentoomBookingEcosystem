using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models
{
    [Table("customer_agreed_terms")]
    public class CustomerAgreedTerms
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int TermsSourceId { get; set; }

        [ForeignKey("TermsSourceId")]
        public CustomerTermsAndConditionsSource TermsSource { get; set; } = null!;
        
        [Required]
        public Guid ReservationGuid { get; set; }
        
        [Required]
        public bool IsAccepted { get; set; }
        
        [Required]
        public DateTime AgreedAt { get; set; } = DateTime.UtcNow;
        
        public int? ClientBitrixId { get; set; }
    }   
}