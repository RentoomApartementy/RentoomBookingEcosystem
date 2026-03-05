using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Upsell;
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
        private IUpsellCatalogService _upsellCatalogService;
        public RentoomOfferService(IIdoOfferService idoOfferService, IApartmentsService apartmentsService, IUpsellCatalogService upsellCatalogService, ILogger<IRentoomOfferService> _logger)
        {
            _idoOfferService = idoOfferService;
            _apartmentsService = apartmentsService;
            _upsellCatalogService = upsellCatalogService;
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
            var regionFilter = query.ApartmentFilterParams?.ApartmentLocationsFilter;
            var addonFilter = query.ApartmentFilterParams?.ApartmentAddonFilter;
            List<ApartmentObject> apartmentsFromAmenities = new();

            
            //KROK 1. filtruje apartementy jesli są wybrane amenities lub regiony lub dodatki
            if ((amenityFilter != null && amenityFilter.Any()) || 
                (regionFilter != null && regionFilter.Any()) ||
                (addonFilter != null && addonFilter.Any()))
            {
                var addonApartmentIds = new List<int>();
                if (addonFilter != null && addonFilter.Any())
                {
                    foreach(var upsellId in addonFilter)
                    {
                        var ids = await _upsellCatalogService.GetApartmentIdsForUpsellAsync(upsellId);
                        if (ids.Any()) addonApartmentIds.AddRange(ids);
                    }
                }

                var apartmentFilter = new ApartmentQueryFilter
                {
                    ApartmentAmenityIds = amenityFilter,
                    ApartmentObjectLocalizationItemRegionNames = regionFilter
                };

                if (addonFilter != null && addonFilter.Any())
                {
                    if (addonApartmentIds.Any())
                    {
                        apartmentFilter.ApartmentIds = addonApartmentIds.Distinct().ToList();
                    }
                }

                apartmentsFromAmenities = await _apartmentsService.GetApartmentsByFilterAsync(apartmentFilter);


                if (apartmentsFromAmenities == null) return null;

                var filteredApartmentIds = apartmentsFromAmenities
                    .Select(apartment => apartment.Id)
                    .Distinct()
                    .ToList();

                idoRequest.ObjectIds = filteredApartmentIds;

                if (filteredApartmentIds.Count == 0)
                {
                    return new RentoomOffer();
                }
            }

            // KROK 2. dla odfiltrowanych apartamentów pobiera oferty z IDOBOOKING zgodnie z filtrami dat i innymi dostepnymi w IDB API

            var offersResponse = await _idoOfferService.GetPricingOffersAsync(idoRequest);
            var pricingOffers = offersResponse?.Result?.PricingOffers ?? new List<PricingOffer>();

            //KROK 3. filtruje pobrane oferty po cenie.
            
            if (query.PriceFilter != null && (query.PriceFilter.PriceFrom>=0 || query.PriceFilter.PriceTo>=0))
            {
                var priceFrom = Convert.ToDecimal(query.PriceFilter.PriceFrom);
                var priceTo = Convert.ToDecimal(query.PriceFilter.PriceTo);

                pricingOffers = pricingOffers
                    .Where(po =>
                        (priceFrom <= 0 || po.MinimalPrice >= priceFrom) &&
                        (priceTo <= 0 || po.MinimalPrice <= priceTo))
                    .ToList();

            }


            var offerIds = pricingOffers.Select(offer => offer.ObjectId).Distinct().ToList();
                      

            //KROK 4. filtruje tylko te apartamentu które znalazły się w ofertach (jeśli nie ma filtrów)
            //Lub zwraca wszystkie pasujące do filtrów (nawet te bez oferty), jeśli filtry są aktywne.
            List<ApartmentObject> apartments;

            if ((amenityFilter != null && amenityFilter.Any()) || 
                (regionFilter != null && regionFilter.Any()) ||
                (addonFilter != null && addonFilter.Any()))
            {
                // Zwracamy wszystkie pasujące do meta-filtrów. 
                // ViewModel zdecyduje które mają oferty i jak je posortować.
                apartments = apartmentsFromAmenities;
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

            
            // KROK 5. zwraca oferty (odfiltrowane lokalnie po cenie) oraz apartamenty
            return new RentoomOffer
            {
                PricingOffers = pricingOffers,
                ApartmentObjects = apartments
            };
        }
    }
}
