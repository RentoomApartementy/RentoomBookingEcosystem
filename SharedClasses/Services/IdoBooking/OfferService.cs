using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{

    public interface IIdoOfferService
    {
        Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request,
            CancellationToken cancellationToken = default);

        Task<List<OfferAvailabilityObject>?> GetAvailabilityAndPricesForDaysAsync(OfferAvailabilityAndPricesParamsSearchInternal request,
            CancellationToken cancellationToken = default);
    }

    public class IdoOfferService :IIdoOfferService
    {
        private readonly IIdoBookingConnectService _idoBookingConnectService;
        private readonly ILogger<IdoOfferService> _logger;

        private const string PricingOffersEndpoint = "public/pricingOffers/34/json";
        private const string AvailabilityAndPricesForDaysEndpoint = "offer/getAvailabilityAndPricesForDays/34/json";

        public IdoOfferService(IIdoBookingConnectService idoBookingConnectService, ILogger<IdoOfferService> logger)
        {
            _idoBookingConnectService = idoBookingConnectService ?? throw new ArgumentNullException(nameof(idoBookingConnectService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request, CancellationToken cancellationToken = default)
        {
         
            _logger.LogInformation("Requesting pricing offers for {ObjectCount} objects between {DateFrom} and {DateTo}.",
                request.ObjectIds?.Count ?? 0, request.DateFrom, request.DateTo);

            var response = await _idoBookingConnectService
                .PostAsync<PricingOffersRequest, PricingOffersResponse>(PricingOffersEndpoint, request, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Errors != null)
            {
                _logger.LogWarning("Pricing offers request returned error {FaultCode}: {FaultString}.",
                    response.Errors.FaultCode, response.Errors.FaultString);
            }

            return response;
        }

        public async Task<List<OfferAvailabilityObject>?> GetAvailabilityAndPricesForDaysAsync(
           OfferAvailabilityAndPricesParamsSearchInternal payload,
           CancellationToken cancellationToken = default)
        {

            var request = new OfferAvailabilityAndPricesForDaysRequest
            {
                Authenticate = _idoBookingConnectService.AuthObjectIdo(),
                ParamsSearch = payload.ParamsSearch,
                Result = new Models.ResultRequestPaging()

            };

            _logger.LogInformation(
                "Requesting availability and prices between {DateFrom} and {DateTo} for {Adults} adults and {Children} children.",
                request.ParamsSearch.DateFrom,
                request.ParamsSearch.DateTo,
                request.ParamsSearch.AdultsNumber,
                request.ParamsSearch.ChildrenNumber ?? 0);

            var response = await _idoBookingConnectService
                .PostAsync<OfferAvailabilityAndPricesForDaysRequest, OfferAvailabilityAndPricesForDaysResponseRoot>(
                    AvailabilityAndPricesForDaysEndpoint,
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

            if (response?.Result.Errors != null)
            {
                _logger.LogWarning(
                    "Availability and prices request returned error {FaultCode}: {FaultString}.",
                    response.Result.Errors.FaultCode,
                    response.Result.Errors.FaultString);
            }


            var ret = response?.Result.OfferObjects;
            if (payload.ObjectIds!= null && payload.ObjectIds.Any())
            {
                var idsHash = payload.ObjectIds.ToHashSet();

                ret = ret.Where(o => idsHash.Contains(o.ObjectId)).ToList();
            }

            return ret;
        }
    }
}
