using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services
{
    
    public interface IRentoomOfferService
    {
        //Task<RentoomOffer> getOffer(RentoomQueryOffer query);
        Task<RentoomOffer> getOfferWitFilter(RentoomQueryOffer query);
    }
    public class RentoomOfferService : IRentoomOfferService
    {

        private IIdoOfferService _idoOfferService;
        private IApartmentsService _apartmentsService;
        public RentoomOfferService(IIdoOfferService idoOfferService, IApartmentsService apartmentsService, ILogger<IRentoomOfferService> _logger)
        {
            _idoOfferService = idoOfferService;
            _apartmentsService = apartmentsService;

        }

        //to be deprecated
        public async Task<RentoomOffer> getOffer(RentoomQueryOffer query)
        {
            var offers = new RentoomOffer();

            var offersfromIdo = await _idoOfferService.GetPricingOffersAsync(query?.IdoOfferParams);
            List<int> ids = offersfromIdo.Result.PricingOffers.Select(r => r.ObjectId).ToList();
            
            var filter = new ApartmentQueryFilter {  ApartmentIds = ids };
            
            var apart = await _apartmentsService.GetApartmentsByFilterAsync(filter);


            offers.PricingOffers = offersfromIdo.Result.PricingOffers;
            offers.ApartmentObjects = apart;

            return offers;

        }

        public async Task<RentoomOffer?> getOfferWitFilter(RentoomQueryOffer query)
        {
            if (query?.IdoOfferParams == null)
            {
                return new RentoomOffer();
            }

            var idoRequest = query.IdoOfferParams;
            var amenityFilter = query.ApartmentFilterParams?.ApartmentAmenitiesFilter;

            List<ApartmentObject> apartmentsFromAmenities = new();

            if (amenityFilter != null && amenityFilter.Any())
            {
                var apartmentFilter = new ApartmentQueryFilter
                {
                   // ApartmentIds = idoRequest.ObjectIds,
                    ApartmentAmenityIds = amenityFilter
                };

                apartmentsFromAmenities = await _apartmentsService.GetApartmentsByFilterAsync(apartmentFilter);


                if (apartmentsFromAmenities == null) return null;

                var filteredApartmentIds = apartmentsFromAmenities
                    .Select(apartment => int.TryParse(apartment.Id, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                idoRequest.ObjectIds = filteredApartmentIds;

                if (filteredApartmentIds.Count == 0)
                {
                    return new RentoomOffer();
                }
            }

            var offersResponse = await _idoOfferService.GetPricingOffersAsync(idoRequest);
            var pricingOffers = offersResponse?.Result?.PricingOffers ?? new List<PricingOffer>();
            var offerIds = pricingOffers.Select(offer => offer.ObjectId).Distinct().ToList();

            List<ApartmentObject> apartments;

            if (amenityFilter != null && amenityFilter.Any())
            {
                apartments = apartmentsFromAmenities
                    .Where(apartment => int.TryParse(apartment.Id, out var id) && offerIds.Contains(id))
                    .ToList();
            }
            else if (offerIds.Count > 0)
            {
                var apartmentFilter = new ApartmentQueryFilter { ApartmentIds = offerIds };
                apartments = await _apartmentsService.GetApartmentsByFilterAsync(apartmentFilter);
            }
            else
            {
                apartments = new List<ApartmentObject>();
            }

            return new RentoomOffer
            {
                PricingOffers = pricingOffers,
                ApartmentObjects = apartments
            };
        }
    }
}
