using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace RentoomBooking.Api.Terms;

public class CustomerAgreedTermsFunction
{
    private readonly CustomerTermsRepository _termsRepository;
    private readonly IReservationWorkflowService _reservationWorkflowService;
    private readonly ILogger<CustomerAgreedTermsFunction> _logger;

    public CustomerAgreedTermsFunction(
        CustomerTermsRepository termsRepository,
        IReservationWorkflowService reservationWorkflowService,
        ILogger<CustomerAgreedTermsFunction> logger)
    {
        _termsRepository = termsRepository ?? throw new ArgumentNullException(nameof(termsRepository));
        _reservationWorkflowService = reservationWorkflowService ?? throw new ArgumentNullException(nameof(reservationWorkflowService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("GetTermsForDisplay")]
    public async Task<HttpResponseData> GetTermsForDisplayAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/terms/get-available")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetTermsForDisplay started at: {Time}", DateTime.UtcNow);

        var response = req.CreateResponse();

        try
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var language = queryParams.Get("language")
                ?? queryParams.Get("lang")
                ?? queryParams.Get("locale")
                ?? queryParams.Get("culture");



            var termsSources = await _termsRepository.GetActiveTermsSourcesAsync(language);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(termsSources), cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetTermsForDisplay.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
        finally
        {
            _logger.LogInformation("GetTermsForDisplay finished at: {Time}", DateTime.UtcNow);
        }
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

            var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var language = queryParams.Get("language")
                ?? queryParams.Get("lang")
                ?? queryParams.Get("locale")
                ?? queryParams.Get("culture");

            var terms = await _termsRepository.GetAgreedTermsByReservationAsync(reservationGuid, language);

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

    [Function("SaveCustomerTerms")]
    public async Task<HttpResponseData> SaveCustomerTermsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/reservations/{reservationTokenGuid}/agreed-terms")] HttpRequestData req,
        string reservationTokenGuid,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("SaveCustomerTerms started at: {Time}", DateTime.UtcNow);

        var response = req.CreateResponse();

        try
        {
            if (!Guid.TryParse(reservationTokenGuid, out var reservationGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("'reservationTokenGuid' must be a valid GUID.", cancellationToken);
                return response;
            }

            var body = await req.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(body))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body cannot be empty.", cancellationToken);
                return response;
            }

            Dictionary<int, bool>? userSelections;
            try
            {
                userSelections = JsonConvert.DeserializeObject<Dictionary<int, bool>>(body);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON payload in SaveCustomerTerms.");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.", cancellationToken);
                return response;
            }

            if (userSelections is null || userSelections.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Payload must contain at least one term selection.", cancellationToken);
                return response;
            }

            await _reservationWorkflowService.SaveCustomerTermsAsync(reservationGuid, userSelections);

            response.StatusCode = HttpStatusCode.NoContent;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SaveCustomerTerms.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
        finally
        {
            _logger.LogInformation("SaveCustomerTerms finished at: {Time}", DateTime.UtcNow);
        }
    }
}
