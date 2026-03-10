using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models
{
    [Table("customer_terms_source_translations")]
    public class CustomerTermsSourceTranslation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TermsSourceId { get; set; }

        [ForeignKey(nameof(TermsSourceId))]
        public CustomerTermsAndConditionsSource TermsSource { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Culture { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Url]
        public string? Link { get; set; }
    }
}
