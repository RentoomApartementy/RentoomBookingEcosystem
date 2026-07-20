using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Models
{
    public class ApartmentSocialMediaDTO
    {
        public int ApartmentItemId { get; set; }
        public string? YouTubeEmbedCode { get; set; }
        public bool YouTubeAutoplay { get; set; }
        public bool YouTubeLoop { get; set; }
        public bool YouTubeMute { get; set; }
        public bool YouTubeControls { get; set; } = true;
        public bool YouTubeModestBranding { get; set; }
        public int? YouTubeWidth { get; set; }
        public int? YouTubeHeight { get; set; }
        public string YouTubeDisplaySize { get; set; } = "M";
        public bool YouTubeVisibleOnRentoom { get; set; } = true;
        public string? InstagramEmbedCode { get; set; }
        public bool InstagramVisibleOnRentoom { get; set; } = true;
    }

    [Table("ApartmentItemSocialMedia", Schema = "rentoom")]
    public class ApartmentItemSocialMedia
    {
        [Key]
        public int ApartmentItemId { get; set; }

        public string? YouTubeEmbedCode { get; set; }
        public bool YouTubeAutoplay { get; set; }
        public bool YouTubeLoop { get; set; }
        public bool YouTubeMute { get; set; }
        public bool YouTubeControls { get; set; } = true;
        public bool YouTubeModestBranding { get; set; }
        public int? YouTubeWidth { get; set; }
        public int? YouTubeHeight { get; set; }
        public string YouTubeDisplaySize { get; set; } = "M";
        public bool YouTubeVisibleOnRentoom { get; set; } = true;
        public string? InstagramEmbedCode { get; set; }
        public bool InstagramVisibleOnRentoom { get; set; } = true;
    }
}
