using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.IdoBooking.Services;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.IdoBooking;

public class DefinedAddonsSyncApi
{
    private readonly IIdoBookingDefinedAddonScrapingService _scrapingService;
    private readonly ILogger<DefinedAddonsSyncApi> _logger;

    public DefinedAddonsSyncApi(
        IIdoBookingDefinedAddonScrapingService scrapingService,
        ILogger<DefinedAddonsSyncApi> logger)
    {
        _scrapingService = scrapingService;
        _logger = logger;
    }

    [Function("SyncDefinedAddonsFromIdoBooking")]
    public async Task<HttpResponseData> Sync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "integrations/idobooking/defined-addons/sync")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var response = req.CreateResponse();

        try
        {
            var addons = await _scrapingService.ScrapeAndPersistAsync(cancellationToken);
            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonSerializer.Serialize(new
            {
                count = addons.Count,
                addons
            }), cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Defined addon scrape/sync failed.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Failed to scrape/sync defined addons from IdoBooking.", cancellationToken);
            return response;
        }
    }
}
