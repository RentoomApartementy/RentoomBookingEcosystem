using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Integrations.RentoomApp
{
    public class TTLockCodeApi
    {
        private readonly RappQrMaintService _qrMaintService;
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly TTLockService _ttLockService;
        private readonly ILogger<TTLockCodeApi> _logger;

        public TTLockCodeApi(RappQrMaintService qrMaintService, PostgresBookingDatabase bookingDatabase, ILogger<TTLockCodeApi> logger, TTLockService ttLockService)
        {
            _ttLockService = ttLockService;
            _qrMaintService = qrMaintService;
            _bookingDatabase = bookingDatabase;
            _logger = logger;
        }

        [Function("GetLockCode")]
        public async Task<HttpResponseData> GetLockCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lockcode/{apartmentItemId:int}")] HttpRequestData req, int apartmentItemId)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("GetLockCode started for apartmentItemId={ApartmentItemId} at {Time}", apartmentItemId, DateTime.UtcNow);

            try
            {
                if (apartmentItemId <= 0)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing or invalid apartmentItemId.");
                    return response;
                }
                var apartmentSettings = await _qrMaintService.GetApartmentItemCodesAsync(apartmentItemId, cancellationToken);
                var lockCode = apartmentSettings?.TTLockId;
                //var lockCode = await _qrMaintService.GetLockCodeAsync(apartmentItemId, cancellationToken);
                if (string.IsNullOrEmpty(lockCode))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No lock code found for the given apartmentItemId.");
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new { lockCode }));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lock code for apartmentItemId={ApartmentItemId}", apartmentItemId);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred while processing your request.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetLockCode finished for apartmentItemId={ApartmentItemId} at {Time}", apartmentItemId, DateTime.UtcNow);
            }
        }

        [Function("PingLockByReservationId")]
        public async Task<HttpResponseData> PingLockByReservationId(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "PingLockByReservationId/{reservationToken}")] HttpRequestData req,
            string reservationToken)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("PingLockByReservationId started for reservationToken={reservationToken} at {Time}", reservationToken, DateTime.UtcNow);

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing or invalid reservationToken.");
                    return response;
                }

                var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(reservationToken, _logger, cancellationToken);
                var objectItemId = reservation?.Reservation?.Items?.FirstOrDefault()?.objectItemId;

                if (objectItemId == null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No reservation found for the given reservationToken.");
                    return response;
                }

                //var lockCode = await _qrMaintService.GetLockCodeAsync(objectItemId.Value, cancellationToken);
                var apartmentSettings = await _qrMaintService.GetApartmentItemCodesAsync(objectItemId.Value, cancellationToken);
                var lockCode = apartmentSettings?.TTLockId;
                if (string.IsNullOrEmpty(lockCode))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync($"No lock code found for the given objectItemId {objectItemId}.");
                    return response;
                }

                /* TTLOCK PING (BATTERY) HERE */
                int? batteryLevel = null;
                string ttlockMessage = "Not a TTLock ID";

                if (int.TryParse(lockCode, out int ttlockId))
                {
                    _logger.LogInformation("Pinging TTLock battery for ID: {LockId}", ttlockId);

                    var batteryResponse = await _ttLockService.GetBatteryLevelAsync(ttlockId);

                    if (batteryResponse.IsSuccess)
                    {
                        batteryLevel = batteryResponse.ElectricQuantity;
                        ttlockMessage = "Success";
                        _logger.LogInformation("Battery level for lock {LockId} is {Level}%", ttlockId, batteryLevel);
                    }
                    else
                    {
                        ttlockMessage = $"TTLock Error: {batteryResponse.ErrMsg}";
                        _logger.LogWarning("Failed to ping TTLock battery: {Error}", batteryResponse.ErrMsg);
                    }
                }

                _logger.LogInformation("Successfully processed ping for reservationToken={reservationToken}, objectItemId={objectItemId}", reservationToken, objectItemId);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");

                await response.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    lockCode,
                    batteryLevel = batteryLevel,
                    status = ttlockMessage,
                    timestamp = DateTime.UtcNow
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinging lock for reservationToken={reservationToken}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred while processing your request.");
                return response;
            }
            finally
            {
                _logger.LogInformation("PingLockByReservationId finished for reservationToken={reservationToken} at {Time}", reservationToken, DateTime.UtcNow);
            }
        }

        [Function("OpenLockByReservationId")]
        public async Task<HttpResponseData> OpenLockByReservationToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "OpenLockByReservationId/{reservationToken}")] HttpRequestData req,
            string reservationToken)
        {
            return await HandleLockAction(req, reservationToken, "Open", (id) => _ttLockService.UnlockAsync(id));
        }

        [Function("CloseLockByReservationId")]
        public async Task<HttpResponseData> CloseLockByReservationToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "CloseLockByReservationId/{reservationToken}")] HttpRequestData req,
            string reservationToken)
        {
            return await HandleLockAction(req, reservationToken, "Close", (id) => _ttLockService.LockAsync(id));
        }

        [Function("GetApartmentItemCodes")]
        public async Task<HttpResponseData> GetApartmentItemAllCodes(
          [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservation/{reservationToken}/apartmentcodes")] HttpRequestData req, string reservationToken)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("GetApartmentItemCodes started for apartmentItemId={reservationToken} at {Time}", reservationToken, DateTime.UtcNow);

            try
            {
                if (String.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing or invalid reservationToken.");
                    return response;
                }

                if (!Guid.TryParse(reservationToken, out var reservationGuid))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Reservation token must resolve to a GUID.", cancellationToken);
                    return response;
                }


                var reservation = await ResolveReservationAsync(reservationToken, cancellationToken);

                if (reservation?.Reservation is null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("Reservation not found.", cancellationToken);
                    return response;
                }


                var apartmentSettings = await _qrMaintService.GetApartmentItemCodesAsync(reservation.Reservation.Items[0].objectItemId, cancellationToken);

                //var lockCode = await _qrMaintService.GetLockCodeAsync(apartmentItemId, cancellationToken);
                if (apartmentSettings == null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No codes found for the given apartmentItemId.");
                    return response;
                }

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(apartmentSettings));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GetApartmentItemCodes for reservationToken={reservationToken}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred while processing your request.");
                return response;
            }
            finally
            {
                _logger.LogInformation("GetApartmentItemCodes finished for reservationToken={reservationToken} at {Time}", reservationToken, DateTime.UtcNow);
            }
        }

        private async Task<RentoomReservation?> ResolveReservationAsync(string reservationToken, CancellationToken cancellationToken)
        {
            var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(reservationToken, _logger, cancellationToken);
            if (reservation is not null) return reservation;

            if (Guid.TryParse(reservationToken, out var reservationGuid))
            {
                var normalizedToken = reservationGuid.ToString("N");
                reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(normalizedToken, _logger, cancellationToken);
                if (reservation is not null) return reservation;
            }

            return null;
        }

        private async Task<HttpResponseData> HandleLockAction(HttpRequestData req, string reservationToken, string actionName, Func<int, Task<RentoomBooking.SharedClasses.Integrations.TTLock.Models.TTLockBaseResponse>> action)
        {
            var cancellationToken = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();
            _logger.LogInformation("{Action} started for reservationToken={Token}", actionName, reservationToken);

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
                    return response;
                }

                var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(reservationToken, _logger, cancellationToken);
                var objectItemId = reservation?.Reservation?.Items?.FirstOrDefault()?.objectItemId;

                if (objectItemId == null)
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("Reservation not found.");
                    return response;
                }

                var lockCode = await _qrMaintService.GetLockCodeAsync(objectItemId.Value, cancellationToken);
                if (string.IsNullOrEmpty(lockCode) || !int.TryParse(lockCode, out int ttlockId))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("Valid TTLock ID not found for this apartment.");
                    return response;
                }

                _logger.LogInformation("Sending {Action} command to TTLock ID: {LockId}", actionName, ttlockId);
                var result = await action(ttlockId);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning("TTLock {Action} failed: {Error}", actionName, result.ErrMsg);
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteAsJsonAsync(new { success = false, message = result.ErrMsg });
                }
                else
                {
                    _logger.LogInformation("TTLock {LockId} {Action} command successful", ttlockId, actionName);
                    response.StatusCode = HttpStatusCode.OK;
                    await response.WriteAsJsonAsync(new { success = true, lockCode, action = actionName });
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during {Action} for token {Token}", actionName, reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                return response;
            }
        }
    }

}