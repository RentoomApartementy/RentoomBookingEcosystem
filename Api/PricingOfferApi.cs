using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.Api
{
    public class OfferApi
    {

        private readonly IOfferService _offerService;
        private readonly ILogger<OfferApi> _logger;
        public OfferApi(IOfferService offerService, ILogger<OfferApi> logger)
        {
            _offerService = offerService ?? throw new ArgumentNullException(nameof(offerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("GetPricingOffers")]
        public async Task<HttpResponseData> GetPricingOffers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "offers/pricing")] HttpRequestData req)
        {
            var response = req.CreateResponse();

            try
            {
                PricingOffersRequest? payload;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        response.StatusCode = HttpStatusCode.BadRequest;
                        await response.WriteStringAsync("Request body can't be empty.").ConfigureAwait(false);
                        return response;
                    }

                    payload = JsonConvert.DeserializeObject<PricingOffersRequest>(body);
                }

                if (payload is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid payload.").ConfigureAwait(false);
                    return response;
                }

                var offers = await _offerService.GetPricingOffersAsync(payload).ConfigureAwait(false);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(offers ?? new PricingOffersResponse())).ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching offers.");
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("Internal server error.").ConfigureAwait(false);
                return response;
            }
        }
    }

}
