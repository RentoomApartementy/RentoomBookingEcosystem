using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models.Cookies
{
    [Table("cookie_notice_sources")]
    public class CookieNoticeSource
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string AppCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Version { get; set; } = string.Empty;

        [Required]
        public bool IsActive { get; set; } = true;

        [Required]
        public DateTime ValidFromUtc { get; set; }

        public DateTime? ValidToUtc { get; set; }

        public List<CookieNoticeTranslation> Translations { get; set; } = new();
    }
}
