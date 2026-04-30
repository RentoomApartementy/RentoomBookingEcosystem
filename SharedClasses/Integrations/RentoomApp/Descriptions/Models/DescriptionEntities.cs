using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Models
{
    [Table("ApartmentDescriptionSets", Schema = "rentoom")]
    public class ApartmentDescriptionSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ApartmentId { get; set; }
        public string SourceLanguage { get; set; } = "pl";
        public bool IsActive { get; set; } = true;
        public string Status { get; set; } = "generated";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string PromptContractJson { get; set; } = string.Empty;
        public string RawAiResponseJson { get; set; } = string.Empty;
        public string RequestPayloadJson { get; set; } = string.Empty;
        public string SystemPromptText { get; set; } = string.Empty;
        public string ResponseSchemaJson { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;

        public virtual ICollection<ApartmentDescriptionVariant> Variants { get; set; } = new List<ApartmentDescriptionVariant>();
        
        [InverseProperty("DescriptionSet")]
        public virtual ICollection<ApartmentDescriptionFaq> FaqItems { get; set; } = new List<ApartmentDescriptionFaq>();
        
        [InverseProperty("DescriptionSet")]
        public virtual ICollection<ApartmentDescriptionHighlight> Highlights { get; set; } = new List<ApartmentDescriptionHighlight>();
        
        [InverseProperty("DescriptionSet")]
        public virtual ICollection<ApartmentDescriptionSeoPhrase> SeoPhrases { get; set; } = new List<ApartmentDescriptionSeoPhrase>();
    }

    [Table("ApartmentDescriptionVariants", Schema = "rentoom")]
    public class ApartmentDescriptionVariant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int DescriptionSetId { get; set; }
        [ForeignKey("DescriptionSetId")]
        public virtual ApartmentDescriptionSet DescriptionSet { get; set; } = null!;

        public string LanguageCode { get; set; } = "pl";
        public string VariantType { get; set; } = string.Empty;
        public bool IsSourceLanguage { get; set; }
        public string TranslationStatus { get; set; } = "source";
        public string H1 { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string MainDescription { get; set; } = string.Empty;
        public string MetaTitle { get; set; } = string.Empty;
        public string MetaDescription { get; set; } = string.Empty;

        public virtual ICollection<ApartmentDescriptionVariantChannel> Channels { get; set; } = new List<ApartmentDescriptionVariantChannel>();
    }

    [Table("ApartmentDescriptionVariantChannels", Schema = "rentoom")]
    public class ApartmentDescriptionVariantChannel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int DescriptionVariantId { get; set; }
        [ForeignKey("DescriptionVariantId")]
        public virtual ApartmentDescriptionVariant DescriptionVariant { get; set; } = null!;

        public string ChannelCode { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    [Table("ApartmentDescriptionFaqItems", Schema = "rentoom")]
    public class ApartmentDescriptionFaq
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int DescriptionSetId { get; set; }
        [ForeignKey("DescriptionSetId")]
        public virtual ApartmentDescriptionSet DescriptionSet { get; set; } = null!;

        public string LanguageCode { get; set; } = "pl";
        public int SortOrder { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }

    [Table("ApartmentDescriptionHighlights", Schema = "rentoom")]
    public class ApartmentDescriptionHighlight
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int DescriptionSetId { get; set; }
        [ForeignKey("DescriptionSetId")]
        public virtual ApartmentDescriptionSet DescriptionSet { get; set; } = null!;

        public string LanguageCode { get; set; } = "pl";
        public int SortOrder { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    [Table("ApartmentDescriptionSeoPhrases", Schema = "rentoom")]
    public class ApartmentDescriptionSeoPhrase
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int DescriptionSetId { get; set; }
        [ForeignKey("DescriptionSetId")]
        public virtual ApartmentDescriptionSet DescriptionSet { get; set; } = null!;

        public string LanguageCode { get; set; } = "pl";
        public int SortOrder { get; set; }
        public string Phrase { get; set; } = string.Empty;
    }

    [Table("ApartmentDescriptionCoverage", Schema = "rentoom")]
    public class ApartmentDescriptionCoverage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ApartmentId { get; set; }
        public string LanguageCode { get; set; } = "pl";
        public int? DescriptionSetId { get; set; }
        public bool HasSourceDescription { get; set; }
        public bool HasFaq { get; set; }
        public bool HasHighlights { get; set; }
        public bool HasSeoPhrases { get; set; }
        public bool HasMainVariant { get; set; }
        public bool HasPremiumVariant { get; set; }
        public bool HasEmotionalVariant { get; set; }
        public string IdBookingSyncStatus { get; set; } = "not_selected";
        public DateTime LastUpdatedAt { get; set; }
    }
}
