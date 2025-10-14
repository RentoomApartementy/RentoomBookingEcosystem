using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBooking.Api;

public class GetAllApartmentObjectsFunction
{

    private readonly IApartmentService _bookingObjectService;

    private readonly ILogger<GetAllApartmentObjectsFunction> _logger;

  

    public GetAllApartmentObjectsFunction(ILogger<GetAllApartmentObjectsFunction> logger, IApartmentService bookingObjectService)
    {
        _logger = logger;
        _bookingObjectService = bookingObjectService;
    }

  // [Function("GetAllApartmentObjectsFunction")]
  //   public async Task SyncFromIdoSell([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
  //   {
  //
  //      
  //
  //   _logger.LogInformation($"SyncObjectsTimer function started at: {DateTime.Now}");
  //
  //       try
  //       {
  //           await _bookingObjectService.GetAllApartmentsFromIdoSellWithLocalizationInfoAsync();
  //       }
  //       catch (Exception ex)
  //       {
  //           _logger.LogError($"An unexpected error occurred during sync: {ex.Message}");
  //       }
  //
  //       _logger.LogInformation($"SyncObjectsTimer function finished at: {DateTime.Now}");
  //   }

}