using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System.Net;

namespace RentoomBooking.Api.Integrations.TTLock
{
    public class TTLockPasscodeFunction
    {
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly RappQrMaintService _qrMaintService;
        private readonly TTLockService _ttLockService;
        private readonly PostgresBookingDbContext _dbContext;
        private readonly IIdoLocksService _idoLocksService;
        private readonly ILogger<TTLockPasscodeFunction> _logger;

        private const int EarlyCheckInAddonId = 40;
        private const int LateCheckOutAddonId = 41;
        private static readonly TimeOnly DefaultCheckInTime = new(15, 0);
        private static readonly TimeOnly DefaultCheckOutTime = new(11, 0);
        private static readonly TimeOnly EarlyCheckInTime = new(14, 0);
        private static readonly TimeOnly LateCheckOutTime = new(12, 0);
        private static readonly TimeSpan CooldownDuration = TimeSpan.FromHours(1);

        public TTLockPasscodeFunction(
            PostgresBookingDatabase bookingDatabase,
            RappQrMaintService qrMaintService,
            TTLockService ttLockService,
            PostgresBookingDbContext dbContext,
            IIdoLocksService idoLocksService,
            ILogger<TTLockPasscodeFunction> logger)
        {
            _bookingDatabase = bookingDatabase;
            _qrMaintService = qrMaintService;
            _ttLockService = ttLockService;
            _dbContext = dbContext;
            _idoLocksService = idoLocksService;
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

        [Function("GetAccessCodes")]
        public async Task<HttpResponseData> GetAccessCodes(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reservation/{reservationToken}/access-codes")]
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

                var model = await BuildAccessCodesResponseAsync(reservationToken, ct);

                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(model));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetAccessCodes for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }

        [Function("GenerateAccessCode")]
        public async Task<HttpResponseData> GenerateAccessCode(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "reservation/{reservationToken}/access-codes/generate")]
            HttpRequestData req,
            string reservationToken)
        {
            var ct = req.FunctionContext.CancellationToken;
            var response = req.CreateResponse();

            _logger.LogInformation("GenerateAccessCode started for token={Token}", reservationToken);

            try
            {
                if (string.IsNullOrWhiteSpace(reservationToken))
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Missing reservationToken.");
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

                var details = reservation.Reservation.ReservationDetails;
                if (details is null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Reservation details missing.");
                    return response;
                }

                var hasEarlyCheckIn = HasAddon(reservation, EarlyCheckInAddonId);
                var hasLateCheckOut = HasAddon(reservation, LateCheckOutAddonId);
                var checkInTime = hasEarlyCheckIn ? EarlyCheckInTime : DefaultCheckInTime;
                var checkOutTime = hasLateCheckOut ? LateCheckOutTime : DefaultCheckOutTime;

                var nowUtc = DateTimeOffset.UtcNow;
                var reservationFrom = details.getDateFrom().Date + checkInTime.ToTimeSpan();
                var reservationTo = details.getDateTo().Date + checkOutTime.ToTimeSpan();
                var nowLocal = nowUtc.LocalDateTime;

                if (nowLocal < reservationFrom || nowLocal > reservationTo)
                {
                    response.StatusCode = HttpStatusCode.Conflict;
                    await response.WriteStringAsync("Generation not allowed outside reservation period.");
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

                var startDate = new DateTimeOffset(
                    nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, TimeSpan.Zero);

                var polandTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
                var localEndDateTime = details.getDateTo().Date + checkOutTime.ToTimeSpan();
                var polandOffset = polandTz.GetUtcOffset(localEndDateTime);
                var endDate = new DateTimeOffset(localEndDateTime, polandOffset);

                var existingForThisHour = await _dbContext.TTLockPasscodes
                    .Where(p => p.ReservationToken == reservationToken && p.StartDate == startDate.ToUniversalTime())
                    .OrderByDescending(p => p.GeneratedAt)
                    .FirstOrDefaultAsync(ct);

                if (existingForThisHour is not null)
                {
                    _logger.LogInformation(
                        "Returning existing passcode {PwdId} for the same hour, token={Token}",
                        existingForThisHour.KeyboardPwdId, reservationToken);

                    var model = await BuildAccessCodesResponseAsync(reservationToken, ct);
                    response.StatusCode = HttpStatusCode.OK;
                    response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                    await response.WriteStringAsync(JsonConvert.SerializeObject(model));
                    return response;
                }

                var lastName = reservation.Reservation?.Client?.LastName ?? "Guest";
                var idoReservationId = reservation.Reservation?.id ?? 0;
                var passcodeName = $"SW-{lastName}-{idoReservationId}";

                var startMs = startDate.ToUnixTimeMilliseconds();
                var endMs = endDate.ToUnixTimeMilliseconds();

                var passcodeResult = await _ttLockService.GetPasscodeAsync(
                    ttlockId, startMs, endMs, passcodeName);

                if (!passcodeResult.IsSuccess || string.IsNullOrWhiteSpace(passcodeResult.KeyboardPwd))
                {
                    _logger.LogWarning("TTLock GetPasscode failed: {Err}", passcodeResult.ErrMsg);
                    response.StatusCode = HttpStatusCode.BadGateway;
                    await response.WriteStringAsync($"TTLock error: {passcodeResult.ErrMsg}");
                    return response;
                }

                var entity = new TTLockPasscodeEntity
                {
                    Id = Guid.NewGuid(),
                    ReservationToken = reservationToken,
                    TTLockId = ttlockId,
                    KeyboardPwdId = passcodeResult.KeyboardPwdId,
                    KeyboardPwd = passcodeResult.KeyboardPwd,
                    PasscodeName = passcodeName,
                    StartDate = startDate.ToUniversalTime(),
                    EndDate = endDate.ToUniversalTime(),
                    GeneratedAt = DateTimeOffset.UtcNow,
                };

                _dbContext.TTLockPasscodes.Add(entity);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Generated passcode {PwdId} for lock {LockId}, token={Token}",
                    entity.KeyboardPwdId, ttlockId, reservationToken);

                var fullModel = await BuildAccessCodesResponseAsync(reservationToken, ct);
                response.StatusCode = HttpStatusCode.OK;
                response.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await response.WriteStringAsync(JsonConvert.SerializeObject(fullModel));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GenerateAccessCode for token={Token}", reservationToken);
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync("An error occurred.");
                return response;
            }
        }

        #region Helpers

        private async Task<AccessCodesResponse> BuildAccessCodesResponseAsync(string reservationToken, CancellationToken ct)
        {
            var codes = new List<AccessCodeDto>();

            var passcodes = await _dbContext.TTLockPasscodes
                .Where(p => p.ReservationToken == reservationToken)
                .OrderByDescending(p => p.GeneratedAt)
                .ToListAsync(ct);

            var distinctPasscodes = passcodes
                .GroupBy(p => p.KeyboardPwdId)
                .Select(g => g.First())
                .ToList();

            foreach (var p in distinctPasscodes)
            {
                codes.Add(new AccessCodeDto
                {
                    Code = p.KeyboardPwd,
                    KeyboardPwdId = p.KeyboardPwdId,
                    GeneratedAt = p.GeneratedAt,
                    ValidFrom = p.StartDate,
                    ValidTo = p.EndDate,
                    Source = "TTLock"
                });
            }

            var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(
                reservationToken, _logger, ct);

            if (reservation?.Reservation?.Items is { Count: > 0 })
            {
                var item = reservation.Reservation.Items[0];
                try
                {
                    var locks = await _idoLocksService.GetLocksAsync(
                        reservation.Reservation.id, item.itemId, ct);
                    var idoCode = locks?.FirstOrDefault()?.Code;
                    if (!string.IsNullOrWhiteSpace(idoCode))
                    {
                        codes.Add(new AccessCodeDto
                        {
                            Code = idoCode,
                            Source = "Ido"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch IDO locks for token={Token}", reservationToken);
                }
            }

            var currentCode = codes.FirstOrDefault();

            var (canGenerate, blockReason, cooldownSeconds, nextAvailableAt) =
                ComputeGenerationEligibility(reservation, distinctPasscodes);

            return new AccessCodesResponse
            {
                CurrentCode = currentCode,
                History = codes,
                CanGenerate = canGenerate,
                GenerationBlockReason = blockReason,
                CooldownSecondsRemaining = cooldownSeconds,
                NextGenerationAvailableAt = nextAvailableAt
            };
        }

        private (bool CanGenerate, string? BlockReason, int? CooldownSeconds, DateTimeOffset? NextAvailableAt)
            ComputeGenerationEligibility(
                RentoomBooking.SharedClasses.Models.RentoomReservation? reservation,
                List<TTLockPasscodeEntity> distinctPasscodes)
        {
            var details = reservation?.Reservation?.ReservationDetails;
            if (details is null)
                return (false, "no_reservation", null, null);

            var hasEarlyCheckIn = HasAddon(reservation!, EarlyCheckInAddonId);
            var hasLateCheckOut = HasAddon(reservation!, LateCheckOutAddonId);
            var checkInTime = hasEarlyCheckIn ? EarlyCheckInTime : DefaultCheckInTime;
            var checkOutTime = hasLateCheckOut ? LateCheckOutTime : DefaultCheckOutTime;

            var now = DateTime.Now;
            var reservationFrom = details.getDateFrom().Date + checkInTime.ToTimeSpan();
            var reservationTo = details.getDateTo().Date + checkOutTime.ToTimeSpan();

            if (now > reservationTo)
                return (false, "after_checkout", null, null);

            if (now < reservationFrom)
                return (false, "before_checkin", null, null);

            var latestPasscode = distinctPasscodes.FirstOrDefault();
            if (latestPasscode is not null)
            {
                var nowUtc = DateTimeOffset.UtcNow;
                var latestStartUtc = latestPasscode.StartDate;
                if (nowUtc.Year == latestStartUtc.Year
                    && nowUtc.Month == latestStartUtc.Month
                    && nowUtc.Day == latestStartUtc.Day
                    && nowUtc.Hour == latestStartUtc.Hour)
                {
                    var nextHourUtc = new DateTimeOffset(
                        nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, TimeSpan.Zero)
                        .AddHours(1);
                    var secondsUntilNextHour = (int)Math.Ceiling((nextHourUtc - nowUtc).TotalSeconds);
                    return (false, "same_hour", secondsUntilNextHour, nextHourUtc);
                }
            }

            return (true, null, null, null);
        }

        private static bool HasAddon(RentoomBooking.SharedClasses.Models.RentoomReservation reservation, int addonId)
        {
            return reservation.Reservation?.Items?
                .SelectMany(item => item.addons ?? [])
                .Any(a => a.addonId == addonId.ToString()) ?? false;
        }

        #endregion
    }
}