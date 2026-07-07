using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Services.Descriptions
{
    public class ApartmentAiDescriptionDto
    {
        public string H1 { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string MainDescription { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;
        public string VariantType { get; set; } = string.Empty;
        public string LanguageCode { get; set; } = string.Empty;

        public List<FaqItemDto> Faqs { get; set; } = new();
        public List<string> Highlights { get; set; } = new();
        public List<string> SeoPhrases { get; set; } = new();
    }

    public class FaqItemDto
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }
}
