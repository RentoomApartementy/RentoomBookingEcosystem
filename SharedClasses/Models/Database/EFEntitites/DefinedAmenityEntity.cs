using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Database.EFEntitites
{
    [Table("defined_amenities")]
    public class DefinedAmenityEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("amenity_id")]
        public int AmenityId { get; set; }

        [Column("amenity_type_name")]
        public string AmenityTypeName { get; set; } = string.Empty;

        [Column("amenity_name")]
        public string AmenityName { get; set; } = string.Empty;

        [Column("lang")]
        public string Lang { get; set; } = "pl";

        [Column("icon_source")]
        public string IconSource { get; set; } = string.Empty;
    }

    public class ApartmentDefinedAmenityDto
    {
        public int Id { get; set; }
        public int AmenityId { get; set; }
        public string AmenityTypeName { get; set; } = string.Empty;
        public string AmenityName { get; set; } = string.Empty;
        public string Lang { get; set; } = "pl";
        public string IconSource { get; set; } = string.Empty;
    }
}
