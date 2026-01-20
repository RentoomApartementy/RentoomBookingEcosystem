using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
    public enum AddonPaymentType
    {
        PayPerPersonPerNight,
        PayPerStay,
        PayPerAmountPerNight,
        PayPerAmount,
        PayPerNight
    }

    public class DefinedAddonDefinition
    {
        public List<LocalizedAddonName> Details { get; set; } = new();
    }

    public class LocalizedAddonName
    {
        public string Lang { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PaymentTypeDescription { get; set; } = string.Empty;
        public string PaymentTypeShortDescription { get; set; } = string.Empty;
    }

}
