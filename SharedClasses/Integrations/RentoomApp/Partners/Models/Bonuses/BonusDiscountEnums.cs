using System.ComponentModel;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses
{
    public enum BonusDiscountValueType
    {
        [Description("Procent")]
        Percent,
        [Description("Kwota")]
        FixedAmount
    }

    public enum BonusDiscountManualStatus
    {
        [Description("Wyłączony")]
        Disabled,
        [Description("Włączony")]
        Enabled
    }

    public enum BonusDiscountLifecycleStatus
    {
        [Description("Nie rozpoczęty")]
        NotStarted,
        [Description("Aktywny")]
        Active,
        [Description("Zakończony")]
        Ended
    }
}
