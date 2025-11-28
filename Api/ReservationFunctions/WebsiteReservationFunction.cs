using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api;

public class WebsiteReservationFunction
{
    private readonly WebsiteReservationService _reservationService;
    private readonly ILogger<WebsiteReservationFunction> _logger;

    public WebsiteReservationFunction(WebsiteReservationService reservationService, ILogger<WebsiteReservationFunction> logger)
    {
        _reservationService = reservationService ?? throw new ArgumentNullException(nameof(reservationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("CreateWebsiteReservation")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "website/reservations")] HttpRequestData req)
    {
        _logger.LogInformation("CreateWebsiteReservation started at: {Time}", DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            string body;
            using (var reader = new StreamReader(req.Body))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            WebsiteCreateReservationRequest? request;
            try
            {
                request = JsonConvert.DeserializeObject<WebsiteCreateReservationRequest>(body);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid website reservation payload.");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.");
                return response;
            }

            if (request is null)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Reservation payload is required.");
                return response;
            }

            var result = await _reservationService.CreateReservationAsync(request, req.FunctionContext.CancellationToken);

            response.StatusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during CreateWebsiteReservation.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("CreateWebsiteReservation finished at: {Time}", DateTime.UtcNow);
        }
    }
}