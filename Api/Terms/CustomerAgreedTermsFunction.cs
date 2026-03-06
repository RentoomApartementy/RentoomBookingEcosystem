using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Terms;

public class CustomerAgreedTermsFunction
{
    private readonly CustomerTermsRepository _termsRepository;
    private readonly ILogger<CustomerAgreedTermsFunction> _logger;

    public CustomerAgreedTermsFunction(CustomerTermsRepository termsRepository, ILogger<CustomerAgreedTermsFunction> logger)
    {
        _termsRepository = termsRepository ?? throw new ArgumentNullException(nameof(termsRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetAgreedTermsByReservation")]
    public async Task<HttpResponseData> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/reservations/{reservationToken}/agreed-terms")] HttpRequestData req,
        string reservationToken,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetAgreedTermsByReservation started at: {Time}", DateTime.UtcNow);

        var response = req.CreateResponse();

        try
        {
            if (!Guid.TryParse(reservationToken, out var reservationGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("'reservationToken' must be a valid GUID.", cancellationToken);
                return response;
            }

            var terms = await _termsRepository.GetAgreedTermsByReservationAsync(reservationGuid);

            if (terms.Count == 0)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("No agreed terms found for the given reservation token.", cancellationToken);
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(terms), cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetAgreedTermsByReservation.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
        finally
        {
            _logger.LogInformation("GetAgreedTermsByReservation finished at: {Time}", DateTime.UtcNow);
        }
    }

    [Function("UpdateAgreedTerm")]
    public async Task<HttpResponseData> PatchAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "db/reservations/{reservationToken}/agreed-terms")] HttpRequestData req,
        string reservationToken,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("UpdateAgreedTerm started at: {Time}", DateTime.UtcNow);

        var response = req.CreateResponse();

        try
        {
            if (!Guid.TryParse(reservationToken, out var reservationGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("'reservationToken' must be a valid GUID.", cancellationToken);
                return response;
            }

            var body = await req.ReadAsStringAsync();
            UpdateAgreedTermRequest? payload;

            try
            {
                payload = JsonConvert.DeserializeObject<UpdateAgreedTermRequest>(body);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON payload in UpdateAgreedTerm.");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.", cancellationToken);
                return response;
            }

            if (payload is null || payload.TermsSourceId <= 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("'termsSourceId' is required and must be greater than 0.", cancellationToken);
                return response;
            }

            var updated = await _termsRepository.UpdateAgreedTermAsync(reservationGuid, payload.TermsSourceId, payload.IsAccepted);

            if (!updated)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync("Agreed term not found for the given reservation token and termsSourceId.", cancellationToken);
                return response;
            }

            response.StatusCode = HttpStatusCode.NoContent;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in UpdateAgreedTerm.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
        finally
        {
            _logger.LogInformation("UpdateAgreedTerm finished at: {Time}", DateTime.UtcNow);
        }
    }
}