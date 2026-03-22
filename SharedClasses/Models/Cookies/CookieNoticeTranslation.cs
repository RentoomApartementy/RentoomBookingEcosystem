using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models.Cookies
{
    [Table("cookie_notice_translations")]
    public class CookieNoticeTranslation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CookieNoticeSourceId { get; set; }

        [ForeignKey(nameof(CookieNoticeSourceId))]
        public CookieNoticeSource CookieNoticeSource { get; set; } = null!;

        [Required]
        [MaxLength(20)]
        public string Culture { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string BannerSummaryHtml { get; set; } = string.Empty;

        [Required]
        public string DetailsHtml { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string AcceptLabel { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string MoreLabel { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LessLabel { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CloseLabel { get; set; } = string.Empty;
    }
}
