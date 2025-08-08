using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.Services;

namespace RentoomBooking.Api;

public class GetAllApartmentObjectsFunction
{

    private readonly IdoSellService _bookingObjectService;

    private readonly ILogger<GetAllApartmentObjectsFunction> _logger;

  

    public GetAllApartmentObjectsFunction(ILogger<GetAllApartmentObjectsFunction> logger, IdoSellService bookingObjectService)
    {
        _logger = logger;
        _bookingObjectService = bookingObjectService;
    }

    [Function("GetAllApartmentObjectsFunction")]
    public async Task SyncFromIdoSell([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {

       

    _logger.LogInformation($"SyncObjectsTimer function started at: {DateTime.Now}");

        try
        {
            await _bookingObjectService.SyncAndStoreObjectsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"An unexpected error occurred during sync: {ex.Message}");
        }

        _logger.LogInformation($"SyncObjectsTimer function finished at: {DateTime.Now}");
    }
}