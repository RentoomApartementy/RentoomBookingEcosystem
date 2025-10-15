using Microsoft.Extensions.Logging;
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
        Task<RentoomOffer> getOffer(RentoomQueryOffer query);
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
    }
}
