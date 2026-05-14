namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses
{
    public class BonusDiscountDto
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
        public BonusDiscountLifecycleStatus LifecycleStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public List<BonusDiscountTargetDto> Targets { get; set; } = new();

    }

    public class BonusDiscountTargetDto
    {
        public int Id { get; set; }
        public int BonusDiscountId { get; set; }
        public int ApartmentItemId { get; set; }
    }


    }
