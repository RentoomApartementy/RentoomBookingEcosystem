using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Services.Upsell;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp;

public class PartnerUpsellApi
{
    private readonly ILogger<PartnerUpsellApi> _logger;
    private readonly IUpsellCatalogService _upsellCatalogService;
    public PartnerUpsellApi(ILogger<PartnerUpsellApi> logger, IUpsellCatalogService upsellCatalogService)
    {
        _logger = logger;
        _upsellCatalogService = upsellCatalogService;
    }

    [Function("GetListOfUpsellServicesx")]
    public async Task<HttpResponseData> GetListOfUpsellServices(
             [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "upsell/{apartmentItemId:int}/{locale}")] HttpRequestData req, int apartmentItemId,string locale)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();
        _logger.LogInformation("GetListOfUpsellServicesx started for apartmentId={ApartmentId} and locale={locale} at {Time}", apartmentItemId,locale, DateTime.UtcNow);

        try
        {
            if (apartmentItemId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Missing or invalid apartmentId.");
                return response;
            }

            var UpselList = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(apartmentItemId, locale, "all", cancellationToken);
            if (UpselList.Count == 0)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("No Upsell options found for the given apartmentId.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(UpselList));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Upsell Options from RentoomApp for apartmentId={ApartmentId}", apartmentItemId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("An error occurred while processing your request.");
            return response;
        }
        finally
        {
            _logger.LogInformation("GetListOfUpsellServicesx finished for apartmentId={ApartmentId} at {Time}", apartmentItemId, DateTime.UtcNow);
        }
    }
}