using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums
{
    public enum PartnerStatus
    {
        Draft,
        Active,
        Archived
    }

    public enum PartnerType
    {
        Restaurant,
        Spa,
        Tour,
        Other
    }

    public enum PartnerServiceStatus
    {
        Draft,
        Active,
        Archived
    }

    public enum PartnerServiceCategory
    {
        [Display(Name = "Śniadania")]
        Breakfast,
        [Display(Name = "Obiady")]
        Lunch,
        [Display(Name = "Kolacje")]
        Dinner,
        [Display(Name = "Masaż")]
        Massage,
        [Display(Name = "Wycieczka")]
        Trip,
        [Display(Name = "Inne")]
        Other
    }

    public enum PartnerServicePricingModel
    {
        [Display(Name = "Na osobę na dzień")]
        PerPersonPerDay,
        [Display(Name = "Na apartament na dzień")]
        PerApartmentPerDay,
        [Display(Name = "Na pobyt")]
        PerStay,
        [Display(Name = "Jednorazowo")]
        OneTime
    }

    public enum PartnerServiceDiscountType
    {
        [Display(Name = "Brak")]
        None,
        [Display(Name = "Procent")]
        Percent,
        [Display(Name = "Stała Kwota")]
        FixedAmount
    }

    public enum PartnerServiceBannerPlacementType
    {
        [Display(Name = "Staywell")]
        StayWell,
        [Display(Name = "Instagram")]
        Instagram,
        [Display(Name = "Facebook")]
        Facebook,
        [Display(Name = "Whatsapp")]
        WhatsApp
    }

    public enum PartnerServiceTargetType
    {
        Apartment
    }
}
