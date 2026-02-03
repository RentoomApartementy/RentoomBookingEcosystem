using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models
{
    [Table("customer_terms_sources")]
    public class CustomerTermsAndConditionsSource
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public DateTime ValidFrom { get; set; }
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Url]
        public string? Link { get; set; }
        
        [Required]
        public bool IsRequired { get; set; } = false;
    }   
}