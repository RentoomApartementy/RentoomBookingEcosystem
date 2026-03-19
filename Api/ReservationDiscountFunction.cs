using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api;

public class ReservationDiscountFunction
{
    private readonly IdoSellService _idoSellService;
    private readonly ILogger<ReservationDiscountFunction> _logger;

    public ReservationDiscountFunction(IdoSellService idoSellService, ILogger<ReservationDiscountFunction> logger)
    {
        _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("SetReservationsDiscountInIdoSell")]
    public async Task<HttpResponseData> SetReservationsDiscountInIdoSell(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "ido/reservations/setdiscount")] HttpRequestData req)
    {
        _logger.LogInformation("SetReservationsDiscountInIdoSell started at: {time}", DateTime.UtcNow);
        var response = req.CreateResponse();

        try
        {
            var requestBody = await ReadBodyAsync(req);
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            var discounts = DeserializeDiscounts(requestBody);
            if (discounts.Count == 0)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Provide at least one reservation in the reservations array.");
                return response;
            }

            var result = await _idoSellService.SetReservationsDiscountAsync(discounts);

            response.StatusCode = result?.Errors is null ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during SetReservationsDiscountInIdoSell.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
        finally
        {
            _logger.LogInformation("SetReservationsDiscountInIdoSell finished at: {time}", DateTime.UtcNow);
        }
    }

    private static List<SetReservationDiscount> DeserializeDiscounts(string requestBody)
    {
        var wrappedRequest = JsonConvert.DeserializeObject<ReservationSetDiscountRequest>(requestBody);
        if (wrappedRequest?.Reservations?.Count > 0)
        {
            return wrappedRequest.Reservations;
        }

        return JsonConvert.DeserializeObject<List<SetReservationDiscount>>(requestBody) ?? new List<SetReservationDiscount>();
    }

    private static async Task<string> ReadBodyAsync(HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }
}
