using RentoomBooking.SharedClasses.Models.IdoBooking;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions
{
    public class ApartmentArrivalInstructionStepDTO
    {
        public int Id { get; set; }
        public int ApartmentItemId { get; set; }
        public int Sequence { get; set; }
        public string Language { get; set; } = "default";
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? ImageMediaAssetId { get; set; }
        public string? ImageUrl { get; set; }
    }

    [Table("ApartmentArrivalInstructionSteps", Schema = "rentoom")]
    public class ApartmentArrivalInstructionStep
    {
        [Key]
        public int Id { get; set; }

        public int ApartmentItemId { get; set; }
        //[NotMapped]
        //public ApartmentObject Apartment { get; set; } = null!;

        [Required]
        public int Sequence { get; set; }

        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = "default";

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        [Required]
        public int? ImageMediaAssetId { get; set; }
    }
}
