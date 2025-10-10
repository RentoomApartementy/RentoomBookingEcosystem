using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{

    public interface IOfferService
    {
        Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request, CancellationToken cancellationToken = default);
    }

    public class OfferService :IOfferService
    {
        private readonly IIdoBookingConnectService _idoBookingConnectService;
        private readonly ILogger<OfferService> _logger;

        private const string PricingOffersEndpoint = "public/pricingOffers/34/json";

        public OfferService(IIdoBookingConnectService idoBookingConnectService, ILogger<OfferService> logger)
        {
            _idoBookingConnectService = idoBookingConnectService ?? throw new ArgumentNullException(nameof(idoBookingConnectService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<PricingOffersResponse?> GetPricingOffersAsync(PricingOffersRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

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
    }
}
