using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Models.Database.EFEntitites
{
    [Table("ttlock_passcodes")]
    public class TTLockPasscodeEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("reservation_token")]
        [Required]
        public string ReservationToken { get; set; } = string.Empty;

        [Column("ttlock_id")]
        public int TTLockId { get; set; }

        [Column("keyboard_pwd_id")]
        public int KeyboardPwdId { get; set; }

        [Column("keyboard_pwd")]
        [Required]
        public string KeyboardPwd { get; set; } = string.Empty;

        [Column("passcode_name")]
        public string? PasscodeName { get; set; }

        [Column("start_date")]
        public DateTimeOffset StartDate { get; set; }

        [Column("end_date")]
        public DateTimeOffset? EndDate { get; set; }

        [Column("generated_at")]
        public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}