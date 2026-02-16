using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint
{
    [Table("QRMaintIdosellMapping", Schema = "rentoom")]
    public class QRMaintIdosellMappingEntity
    {
        [Key]
        public int Id { get; set; }
        public int QRMaintId { get; set; }
        public int IDOSellApartmentId { get; set; }
        public bool IsApartmentDefaultWarehouse { get; set; }
        public string QRMaintApartmentType { get; set; }
        public string QrMaintFAId { get; set; }
    }

    [Table("RentoomQRs", Schema = "rentoom")]
    public class RentoomQREntity
    {
        [Key]
        public int ApartmentItemId { get; set; }
        public string QrCodesJson { get; set; }
    }

    [Table("ApartmentItemLocalSettings", Schema = "rentoom")]
    public class ApartmentItemLocalSettings
    {
        [Key]
        public int ApartmentItemId { get; set; }

        [Column("TTLockId")]
        public string? TTLockId { get; set; }
        public string? GateCode { get; set; }
        public string? GateDoorCode { get; set; }
        public string? AdditionalDoorCode { get; set; }
        public string? StoreroomCode { get; set; }
    }
}