using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Upsell
{
    public sealed record ReservationPricingContext
    {
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public int Adults { get; init; }
        public int Children { get; init; }
        public string Currency { get; init; } = string.Empty;
        public int Nights => Math.Max(0, EndDate.DayNumber - StartDate.DayNumber);
        public int TotalGuests => Adults + Children;
    }

    public static class UpsellPricingCalculator
    {
        public static decimal CalculateTotal(
            PartnerServicePricingModel pricingModel,
            decimal unitPriceGross,
            int nights,
            int totalGuests,
            int quantity)
        {
            return pricingModel switch
            {
                PartnerServicePricingModel.PerPersonPerDay => unitPriceGross * nights * totalGuests * quantity,
                PartnerServicePricingModel.PerApartmentPerDay => unitPriceGross * nights * quantity,
                PartnerServicePricingModel.PerStay => unitPriceGross * quantity,
                PartnerServicePricingModel.OneTime => unitPriceGross * quantity,
                _ => throw new ArgumentOutOfRangeException(nameof(pricingModel), pricingModel, "Unsupported pricing model.")
            };
        }
    }

}
