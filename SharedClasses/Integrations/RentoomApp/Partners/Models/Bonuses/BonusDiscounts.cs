using System.ComponentModel.DataAnnotations.Schema;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses
{
    [Table("BonusDiscounts", Schema = "rentoom")]
    public class BonusDiscount
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public BonusDiscountValueType ValueType { get; set; }
        public decimal Value { get; set; }
        public decimal? MinimumOrderGrossAmount { get; set; }
        public int? MinimumReservationDays { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public BonusDiscountManualStatus ManualStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        public ICollection<BonusDiscountTarget> Targets { get; set; } = new List<BonusDiscountTarget>();
    }

    [Table("BonusDiscountTargets", Schema = "rentoom")]
    public class BonusDiscountTarget
    {
        public int Id { get; set; }
        public int BonusDiscountId { get; set; }
        public BonusDiscount BonusDiscount { get; set; } = null!;
        public int ApartmentItemId { get; set; }
    }
}
