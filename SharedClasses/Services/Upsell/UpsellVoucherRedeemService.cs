using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellVoucherRedeemService
    {
        Task<RedeemResultDto> TryRedeemByCodeShortAsync(string codeShort);
        Task<RedeemResultDto> TryRedeemByQrTokenAsync(string qrToken);
    }

    public class UpsellVoucherRedeemService : IUpsellVoucherRedeemService
    {
        private const string FailureNotFound = "NotFound";
        private const string FailureExpired = "Expired";
        private const string FailureOutsideReservationWindow = "OutsideReservationWindow";
        private const string FailureLimitReached = "LimitReached";
        private const string FailureCancelled = "Cancelled";

        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
      

        public UpsellVoucherRedeemService(IDbContextFactory<PostgresBookingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
          
        }

        public async Task<RedeemResultDto> TryRedeemByCodeShortAsync(string codeShort)
        {
            if (string.IsNullOrWhiteSpace(codeShort))
            {
                return BuildFailure(FailureNotFound);
            }

          

         
            if (string.IsNullOrWhiteSpace(codeShort))
            {
                return BuildFailure(FailureNotFound);
            }

            await using var context = _dbContextFactory.CreateDbContext();
            var result = await FindByCodeShortAsync(context, codeShort);
            return await TryRedeemInternalAsync(context, result.voucher, result.line);
        }

        public async Task<RedeemResultDto> TryRedeemByQrTokenAsync(string qrToken)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
            {
                return BuildFailure(FailureNotFound);
            }

           

            var trimmed = qrToken.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return BuildFailure(FailureNotFound);
            }

            await using var context = _dbContextFactory.CreateDbContext();
            var result = await FindByQrTokenAsync(context, trimmed);
            return await TryRedeemInternalAsync(context, result.voucher, result.line);
        }

        private async Task<RedeemResultDto> TryRedeemInternalAsync(
            PostgresBookingDbContext context,
            UpsellVoucherEntity? voucher,
            UpsellOrderLineEntity? line)
        {
            if (voucher is null || line is null)
            {
                return BuildFailure(FailureNotFound);
            }

            var failureReason = EvaluateFailureReason(voucher);
            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                return BuildFailure(failureReason, voucher, line);
            }

            var now = DateTime.UtcNow;
            var affected = await context.UpsellVouchers
                                         .Where(v =>
                                             v.UpsellVoucherGuid == voucher.UpsellVoucherGuid &&
                                             v.Status == UpsellVoucherStatuses.Active &&
                                             (!v.MaxUses.HasValue || v.UsedCount < v.MaxUses.Value))
                                         .ExecuteUpdateAsync(setters => setters
                                             .SetProperty(v => v.UsedCount, v => v.UsedCount + 1)
                                             .SetProperty(v => v.LastUsedAtUtc, v => now)
                                             .SetProperty(v => v.UpdatedAt, v => now));
            if (affected == 1)
            {
                var refreshed = await FindByGuidAsync(context, voucher.UpsellVoucherGuid);
                if (refreshed.voucher is null || refreshed.line is null)
                {
                    return BuildFailure(FailureNotFound);
                }

                return BuildSuccess(refreshed.voucher, refreshed.line);
            }

            var latest = await FindByGuidAsync(context, voucher.UpsellVoucherGuid);
            if (latest.voucher is null || latest.line is null)
            {
                return BuildFailure(FailureNotFound);
            }

            var latestReason = EvaluateFailureReason(latest.voucher) ?? FailureLimitReached;
            return BuildFailure(latestReason, latest.voucher, latest.line);
        }

        private static string? EvaluateFailureReason(UpsellVoucherEntity voucher)
        {
            if (!string.Equals(voucher.Status, UpsellVoucherStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(voucher.Status, UpsellVoucherStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
                {
                    return FailureCancelled;
                }

                if (string.Equals(voucher.Status, UpsellVoucherStatuses.Expired, StringComparison.OrdinalIgnoreCase))
                {
                    return FailureExpired;
                }

                if (string.Equals(voucher.Status, UpsellVoucherStatuses.Completed, StringComparison.OrdinalIgnoreCase))
                {
                    return FailureLimitReached;
                }

                return FailureExpired;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (today < voucher.ValidFrom || today > voucher.ValidTo)
            {
                return FailureOutsideReservationWindow;
            }

            if (voucher.MaxUses.HasValue && voucher.UsedCount >= voucher.MaxUses.Value)
            {
                return FailureLimitReached;
            }

            return null;
        }

        private static RedeemResultDto BuildSuccess(UpsellVoucherEntity voucher, UpsellOrderLineEntity line)
        {
            return new RedeemResultDto
            {
                Success = true,
                FailureReason = null,
                UpdatedUsedCount = voucher.UsedCount,
                MaxUses = voucher.MaxUses,
                ReservationGuid = voucher.ReservationGuid,
                PartnerServiceId = line.PartnerServiceId,
                TitleSnapshot = line.TitleSnapshot
            };
        }

        private static RedeemResultDto BuildFailure(
            string reason,
            UpsellVoucherEntity? voucher = null,
            UpsellOrderLineEntity? line = null)
        {
            return new RedeemResultDto
            {
                Success = false,
                FailureReason = reason,
                UpdatedUsedCount = voucher?.UsedCount ?? 0,
                MaxUses = voucher?.MaxUses,
                ReservationGuid = voucher?.ReservationGuid ?? Guid.Empty,
                PartnerServiceId = line?.PartnerServiceId ?? 0,
                TitleSnapshot = line?.TitleSnapshot ?? string.Empty
            };
        }

        private static async Task<(UpsellVoucherEntity? voucher, UpsellOrderLineEntity? line)> FindByGuidAsync(
            PostgresBookingDbContext context,
            Guid voucherGuid)
        {
            var result = await (from voucher in context.UpsellVouchers.AsNoTracking()
                                join line in context.UpsellOrderLines.AsNoTracking()
                                    on voucher.UpsellOrderLineGuid equals line.UpsellOrderLineGuid
                                where voucher.UpsellVoucherGuid == voucherGuid
                                select new { voucher, line })
                .FirstOrDefaultAsync();

            return (result?.voucher, result?.line);
        }

        private static async Task<(UpsellVoucherEntity? voucher, UpsellOrderLineEntity? line)> FindByCodeShortAsync(
            PostgresBookingDbContext context,
            string codeShort)
        {
            var result = await (from voucher in context.UpsellVouchers.AsNoTracking()
                                join line in context.UpsellOrderLines.AsNoTracking()
                                    on voucher.UpsellOrderLineGuid equals line.UpsellOrderLineGuid
                                where voucher.CodeShort == codeShort
                                select new { voucher, line })
                .FirstOrDefaultAsync();

            return (result?.voucher, result?.line);
        }

        private static async Task<(UpsellVoucherEntity? voucher, UpsellOrderLineEntity? line)> FindByQrTokenAsync(
            PostgresBookingDbContext context,
            string qrToken)
        {
            var result = await (from voucher in context.UpsellVouchers.AsNoTracking()
                                join line in context.UpsellOrderLines.AsNoTracking()
                                    on voucher.UpsellOrderLineGuid equals line.UpsellOrderLineGuid
                                where voucher.QrToken == qrToken
                                select new { voucher, line })
                .FirstOrDefaultAsync();

            return (result?.voucher, result?.line);
        }

     
    }
}
