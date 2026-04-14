using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System.Net;

namespace RentoomBooking.Api.Integrations.RentoomApp
{
    public class TTLockPasscodeFunction
    {
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly RappQrMaintService _qrMaintService;
        private readonly TTLockService _ttLockService;
        private readonly PostgresBookingDbContext _dbContext;
        private readonly ILogger<TTLockPasscodeFunction> _logger;

        public TTLockPasscodeFunction(
            PostgresBookingDatabase bookingDatabase,
            RappQrMaintService qrMaintService,
            TTLockService ttLockService,
            PostgresBookingDbContext dbContext,
            ILogger<TTLockPasscodeFunction> logger)
        {
            _bookingDatabase = bookingDatabase;
            _qrMaintService = qrMaintService;
            _ttLockService = ttLockService;
            _dbContext = dbContext;
            _logger = logger;
        }

        private sealed record GeneratePasscodeRequestBody(
            DateTimeOffset StartDate,
            DateTimeOffset EndDate,
            string PasscodeName);

        [Function("GenerateTTLockPasscode")]
        public async Task<HttpResponseData> GeneratePasscode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservation/{reservationToken}/passcode/generate")]
            HttpRequestData req,
            string reservationToken)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation("GenerateTTLockPasscode started for token={Token}", reservationToken);

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
                    return response;
                }

                var body = await req.ReadFromJsonAsync<GeneratePasscodeRequestBody>();
                if (body is null || string.IsNullOrWhiteSpace(body.PasscodeName))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing request body (StartDate, EndDate, PasscodeName required).");
                    return response;
                }

                var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(
                    reservationToken, _logger, ct);

                if (reservation?.Reservation?.Items is not { Count: > 0 })
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("Reservation not found.");
                    return response;
                }

                var objectItemId = reservation.Reservation.Items[0].objectItemId;
                var apartmentSettings = await _qrMaintService.GetApartmentItemCodesAsync(objectItemId, ct);
                var lockCodeStr = apartmentSettings?.TTLockId;

                if (string.IsNullOrEmpty(lockCodeStr) || !int.TryParse(lockCodeStr, out var ttlockId))
                {
                    response.StatusCode = HttpStatusCode.NotFound;
                    await response.WriteStringAsync("No valid TTLock ID found for this apartment.");
                    return response;
                 }

                var existingForThisHour = await _dbContext.TTLockPasscodes
                    .Where(p => p.ReservationToken == reservationToken && p.StartDate == body.StartDate.ToUniversalTime())
                    .OrderByDescending(p => p.GeneratedAt)
                    .FirstOrDefaultAsync(ct);

                if (existingForThisHour is not null)
                {
                    _logger.LogInformation(
                        "Returning existing passcode {PwdId} for the same hour, token={Token}",
                        existingForThisHour.KeyboardPwdId, reservationToken);

                    response.StatusCode = HttpStatusCode.OK;
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(JsonConvert.SerializeObject(new
                    {
                        keyboardPwd = existingForThisHour.KeyboardPwd,
                        keyboardPwdId = existingForThisHour.KeyboardPwdId,
                        generatedAt = existingForThisHour.GeneratedAt,
                        startDate = existingForThisHour.StartDate,
                        endDate = existingForThisHour.EndDate,
                    }));
                    return response;
                }

                var startMs = body.StartDate.ToUnixTimeMilliseconds();
                var endMs = body.EndDate.ToUnixTimeMilliseconds();

                var passcodeResult = await _ttLockService.GetPasscodeAsync(
                    ttlockId, startMs, endMs, body.PasscodeName);

                if (!passcodeResult.IsSuccess || string.IsNullOrWhiteSpace(passcodeResult.KeyboardPwd))
                {
                    _logger.LogWarning("TTLock GetPasscode failed: {Err}", passcodeResult.ErrMsg);
                    response.StatusCode = HttpStatusCode.BadGateway;
                    await response.WriteStringAsync($"TTLock error: {passcodeResult.ErrMsg}");
                    return response;
                }

                // Zapisz w DB
                var entity = new TTLockPasscodeEntity
                {
                    Id = Guid.NewGuid(),
                    ReservationToken = reservationToken,
                    TTLockId = ttlockId,
                    KeyboardPwdId = passcodeResult.KeyboardPwdId,
                    KeyboardPwd = passcodeResult.KeyboardPwd,
                    PasscodeName = body.PasscodeName,
                    StartDate = body.StartDate.ToUniversalTime(),
                    EndDate = body.EndDate.ToUniversalTime(),
                    GeneratedAt = DateTimeOffset.UtcNow,
                };

                _dbContext.TTLockPasscodes.Add(entity);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Generated passcode {PwdId} for lock {LockId}, token={Token}",
                    entity.KeyboardPwdId, ttlockId, reservationToken);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(new
                {
                    keyboardPwd = entity.KeyboardPwd,
                    keyboardPwdId = entity.KeyboardPwdId,
                    generatedAt = entity.GeneratedAt,
                    startDate = entity.StartDate,
                    endDate = entity.EndDate,
                }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating passcode for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }

        [Function("GetTTLockPasscodes")]
        public async Task<HttpResponseData> GetPasscodes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservation/{reservationToken}/passcode/history")]
            HttpRequestData req,
            string reservationToken)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
                    return response;
                }

                var passcodes = _dbContext.TTLockPasscodes
                    .Where(p => p.ReservationToken == reservationToken)
                    .OrderByDescending(p => p.GeneratedAt)
                    .Select(p => new
                    {
                        p.KeyboardPwd,
                        p.KeyboardPwdId,
                        p.GeneratedAt,
                        p.StartDate,
                        p.EndDate,
                    })
                    .ToList();

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(passcodes));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching passcodes for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }
    }
}