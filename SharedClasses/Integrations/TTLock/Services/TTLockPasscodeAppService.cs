using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System.Net;

namespace RentoomBooking.SharedClasses.Integrations.TTLock.Services
{
    public class TTLockPasscodeAppService : ITTLockPasscodeAppService
    {
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly RappQrMaintService _qrMaintService;
        private readonly TTLockService _ttLockService;
        private readonly PostgresBookingDbContext _dbContext;
        private readonly IIdoLocksService _idoLocksService;
        private readonly ILogger<TTLockPasscodeAppService> _logger;

        private const int EarlyCheckInAddonId = 40;
        private const int LateCheckOutAddonId = 41;
        private static readonly TimeOnly DefaultCheckInTime = new(15, 0);
        private static readonly TimeOnly DefaultCheckOutTime = new(11, 0);
        private static readonly TimeOnly EarlyCheckInTime = new(14, 0);
        private static readonly TimeOnly LateCheckOutTime = new(12, 0);

        public TTLockPasscodeAppService(
            PostgresBookingDatabase bookingDatabase,
            RappQrMaintService qrMaintService,
            TTLockService ttLockService,
            PostgresBookingDbContext dbContext,
            IIdoLocksService idoLocksService,
            ILogger<TTLockPasscodeAppService> logger)
        {
            _bookingDatabase = bookingDatabase;
            _qrMaintService = qrMaintService;
            _ttLockService = ttLockService;
            _dbContext = dbContext;
            _idoLocksService = idoLocksService;
            _logger = logger;
        }

        public async Task<TTLockPasscodeOperationResult> GetAccessCodesAsync(string reservationToken, CancellationToken ct)
        {
            var model = await BuildAccessCodesResponseAsync(reservationToken, ct);
            return new TTLockPasscodeOperationResult
            {
                StatusCode = HttpStatusCode.OK,
                Payload = model
            };
        }

        public async Task<TTLockPasscodeOperationResult> GenerateAccessCodeAsync(string reservationToken, CancellationToken ct)
        {
            _logger.LogInformation("GenerateAccessCode started for token={Token}", reservationToken);

            var reservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(
                reservationToken, _logger, ct);

            if (reservation?.Reservation?.Items is not { Count: > 0 })
            {
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorMessage = "Reservation not found."
                };
            }

            var details = reservation.Reservation.ReservationDetails;
            if (details is null)
            {
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorMessage = "Reservation details missing."
                };
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
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.Conflict,
                    ErrorMessage = "Generation not allowed outside reservation period."
                };
            }

            var objectItemId = reservation.Reservation.Items[0].objectItemId;
            var apartmentSettings = await _qrMaintService.GetApartmentItemCodesAsync(objectItemId, ct);
            var lockCodeStr = apartmentSettings?.TTLockId;

            if (string.IsNullOrEmpty(lockCodeStr) || !int.TryParse(lockCodeStr, out var ttlockId))
            {
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.NotFound,
                    ErrorMessage = "No valid TTLock ID found for this apartment."
                };
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

                var existingModel = await BuildAccessCodesResponseAsync(reservationToken, ct);
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.OK,
                    Payload = existingModel
                };
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
                return new TTLockPasscodeOperationResult
                {
                    StatusCode = HttpStatusCode.BadGateway,
                    ErrorMessage = $"TTLock error: {passcodeResult.ErrMsg}"
                };
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
            return new TTLockPasscodeOperationResult
            {
                StatusCode = HttpStatusCode.OK,
                Payload = fullModel
            };
        }

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
    }
}
