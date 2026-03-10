using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models
{
    [Table("customer_terms_sources")]
    public class CustomerTermsAndConditionsSource
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public DateTime ValidFrom { get; set; }

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;
        
        [MaxLength(500)]
        public string? Description { get; set; }
        
        [Url]
        public string? Link { get; set; }

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public int SortOrder { get; set; } = 0;
        
        [Required]
        public bool IsRequired { get; set; } = false;

        public List<CustomerTermsSourceTranslation> Translations { get; set; } = new();
    }   
}
