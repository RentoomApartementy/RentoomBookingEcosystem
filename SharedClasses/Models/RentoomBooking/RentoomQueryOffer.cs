using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.RentoomBooking
{
    public class RentoomQueryOffer
    {
        public PricingOffersRequest IdoOfferParams { get; set; }
        public ApartmentFilters? ApartmentFilterParams { get; set; };
    }

    public class RentoomOffer {
        public List<PricingOffer> PricingOffers { get; set; } = [];
        public List<ApartmentObject> ApartmentObjects { get; set; } = [];
    }
    public class ApartmentFilters {
        public List<int>? ApartmentAddonFilter { get; set; }
        public List<int>? ApartmentAmenitiesFilter {get;set;}
        public List<int>? ApartmentLocationsFilter { get; set; }

    }


}
