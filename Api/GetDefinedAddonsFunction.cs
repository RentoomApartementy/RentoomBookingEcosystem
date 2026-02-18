using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;

namespace RentoomBooking.Api;

public class GetDefinedAddonsFunction
{
    private readonly ApartmentRepository _apartmentRepository;
    private readonly ILogger<GetDefinedAddonsFunction> _logger;

    public GetDefinedAddonsFunction(
        ApartmentRepository apartmentRepository,
        ILogger<GetDefinedAddonsFunction> logger)
    {
        _apartmentRepository = apartmentRepository;
        _logger = logger;
    }

    [Function("GetDefinedAddons")]
    public async Task<HttpResponseData> GetDefinedAddons(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/definedaddons")] HttpRequestData req)
    {
        _logger.LogInformation("GetDefinedAddons started at: {time}", DateTime.UtcNow);

        var res = req.CreateResponse();

        try
        {
            var addons = await _apartmentRepository.GetDefinedAddonsAsync();

            res.StatusCode = HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(addons));
            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetDefinedAddons.");
            res.StatusCode = HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.");
            return res;
        }
        finally
        {
            _logger.LogInformation("GetDefinedAddons finished at: {time}", DateTime.UtcNow);
        }
    }
}