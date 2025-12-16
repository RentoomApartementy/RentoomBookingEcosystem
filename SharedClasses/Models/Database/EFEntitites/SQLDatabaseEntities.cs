using RentoomBooking.SharedClasses.Models.StayWell;
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

        [Column("reservation_id")]
        public int ReservationId { get; set; }

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

    public class TermsEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;
        [Column("version_accepted")]
        public string VersionAccepted { get; set; } = string.Empty;
        [Column("type_accepted")]
        public string TypeAccepted { get; set; } = string.Empty;
        [Column("accepted_at")]
        public DateTime AcceptedAt { get; set; }
        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
            = DateTime.UtcNow;
    }

    public class RegistrationCardEntity
    {
        [Key]
        [Column("res_token")]
        public string ResToken { get; set; } = string.Empty;

        [Column("contact_email")]
        public string ContactEmail { get; set; } = string.Empty;

        //[Column("contact_phone")]
        //public string ContactPhone { get; set; } = string.Empty;
        //[Column("phone_country_code")]
        //public string PhoneCountryCode { get; set; } = string.Empty;
        [Column("check_in_time")]
        public DateTime CheckInTime { get; set; }
        [Column("guests_data")]
        public List<RegistrationCardGuestModel> GuestsData { get; set; } = new();

    }

}
