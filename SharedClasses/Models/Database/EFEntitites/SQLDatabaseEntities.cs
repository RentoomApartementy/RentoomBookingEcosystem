using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Database.EFEntitites
{
    public class ApartmentInfoEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ApartmentAmenityEntity
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ApartmentHashEntity
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = string.Empty;

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class ReservationEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;

        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;
    }
    public class SearchFiltersEntity
    {
        [Key]
        [Column("filter_groupname")]
        public string FilterGroupName { get; set; } = string.Empty;
        
        [Column("payload", TypeName = "jsonb")]
        public string Payload { get; set; } = string.Empty;

    }
}
