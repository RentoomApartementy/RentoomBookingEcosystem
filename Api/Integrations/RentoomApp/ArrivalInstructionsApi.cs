using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp;

public class ArrivalInstructionsApi
{
    private readonly ILogger<ArrivalInstructionsApi> _logger;
    private readonly ArrivalInstructionsService _arrivalInstructionsService;

    public ArrivalInstructionsApi(
        ILogger<ArrivalInstructionsApi> logger,
        ArrivalInstructionsService arrivalInstructionsService)
    {
        _logger = logger;
        _arrivalInstructionsService = arrivalInstructionsService;
    }

    [Function("GetArrivalInstructionStepsForApartment")]
    public async Task<HttpResponseData> GetArrivalInstructionSteps(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartment/arrivalinstructions/{apartmentId:int}")] HttpRequestData req,
        int apartmentId)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var language = queryParams.Get("language")
            ?? queryParams.Get("lang")
            ?? queryParams.Get("locale")
            ?? queryParams.Get("culture");

        _logger.LogInformation(
            "GetArrivalInstructionSteps started for apartmentId={ApartmentId}, language={Language} at {Time}",
            apartmentId,
            language,
            DateTime.UtcNow);

        try
        {
            if (apartmentId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Missing or invalid apartmentId.");
                return response;
            }

            var steps = await _arrivalInstructionsService.GetArrivalInstructionStepsAsync(apartmentId, language, cancellationToken);
            if (steps.Count == 0)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("No arrival instructions found for the given apartmentId.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(steps));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching arrival instructions for apartmentId={ApartmentId}", apartmentId);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("An error occurred while processing your request.");
            return response;
        }
        finally
        {
            _logger.LogInformation("GetArrivalInstructionSteps finished for apartmentId={ApartmentId} at {Time}", apartmentId, DateTime.UtcNow);
        }
    }
}
