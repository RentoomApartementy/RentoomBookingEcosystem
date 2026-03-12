using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellVoucherProvisioningService
    {
        Task EnsureForOrderAsync(Guid upsellOrderGuid);
        Task EnsureForReservationAsync(Guid reservationGuid);
    }

    public class UpsellVoucherProvisioningService : IUpsellVoucherProvisioningService
    {
        private const int MaxTokenRetries = 3;
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly IUpsellOrderStore _orderStore;
        private readonly IReservationStore _reservationStore;
        private readonly IUpsellVoucherCodeGenerator _codeGenerator;
        private readonly ILogger<UpsellVoucherProvisioningService> _logger;
        private readonly Task _initializationTask;

        public UpsellVoucherProvisioningService(
            IDbContextFactory<PostgresBookingDbContext> dbContextFactory,
            IUpsellOrderStore orderStore,
            IReservationStore reservationStore,
            IUpsellVoucherCodeGenerator codeGenerator,
            ILogger<UpsellVoucherProvisioningService> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _orderStore = orderStore ?? throw new ArgumentNullException(nameof(orderStore));
            _reservationStore = reservationStore ?? throw new ArgumentNullException(nameof(reservationStore));
            _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           
        }

        public async Task EnsureForOrderAsync(Guid upsellOrderGuid)
        {
            //// await _initializationTask;

            var record = await _orderStore.GetAsync(upsellOrderGuid);
            if (record is null)
            {
                _logger.LogWarning("Upsell order {OrderGuid} not found while provisioning vouchers.", upsellOrderGuid);
                return;
            }

            var reservationGuid = record.State.Request?.ReservationGuid ?? await GetReservationGuidAsync(upsellOrderGuid);
            if (!reservationGuid.HasValue || reservationGuid.Value == Guid.Empty)
            {
                _logger.LogWarning("Upsell order {OrderGuid} is missing reservation guid; skipping voucher provisioning.", upsellOrderGuid);
                return;
            }

            var reservation = await _reservationStore.GetAsync(reservationGuid.Value);
            var startRequest = reservation?.State?.StartRequest;
            if (startRequest is null)
            {
                _logger.LogWarning("Reservation {ReservationGuid} not found for upsell order {OrderGuid}.", reservationGuid, upsellOrderGuid);
                return;
            }

            var validFrom = startRequest.StartDate;
            var validTo = startRequest.EndDate;

            foreach (var line in record.Lines)
            {
                await EnsureVoucherForLineAsync(line, reservationGuid.Value, validFrom, validTo);
            }
        }

        public async Task EnsureForReservationAsync(Guid reservationGuid)
        {
            //// await _initializationTask;

            var orders = await _orderStore.GetByReservationGuidAsync(reservationGuid);
            var paidOrders = orders.Where(order => string.Equals(order.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase));

            foreach (var order in paidOrders)
            {
                await EnsureForOrderAsync(order.UpsellOrderGuid);
            }
        }

        private async Task EnsureVoucherForLineAsync(UpsellOrderLineRecord line, Guid reservationGuid, DateOnly validFrom, DateOnly validTo)
        {
            for (var attempt = 0; attempt < MaxTokenRetries; attempt++)
            {
                await using var context = _dbContextFactory.CreateDbContext();

                var existing = await context.UpsellVouchers.AsNoTracking()
                    .FirstOrDefaultAsync(voucher => voucher.UpsellOrderLineGuid == line.UpsellOrderLineGuid);

                if (existing is not null)
                {
                    return;
                }

                var qrToken = _codeGenerator.GenerateQrToken();
                var codeShort = _codeGenerator.DeriveShortCode(qrToken);
                var maxUses = DetermineMaxUses(line, validFrom, validTo);

                var entity = new UpsellVoucherEntity
                {
                    UpsellVoucherGuid = Guid.NewGuid(),
                    UpsellOrderLineGuid = line.UpsellOrderLineGuid,
                    ReservationGuid = reservationGuid,
                    QrToken = qrToken,
                    CodeShort = codeShort,
                    Status = UpsellVoucherStatuses.Active,
                    MaxUses = maxUses,
                    UsedCount = 0,
                    ValidFrom = validFrom,
                    ValidTo = validTo,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.UpsellVouchers.Add(entity);

                try
                {
                    await context.SaveChangesAsync();
                    return;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning("Failed to provision voucher for upsell order line {LineGuid} due to token collisions. {ex}", line.UpsellOrderLineGuid, ex.Message);
                    throw;
                }
                /* when (IsUniqueViolation(ex, out var constraintName))
                {
                      if (IsOrderLineConstraint(constraintName))
                    {
                        await LoadExistingVoucherAsync(line.UpsellOrderLineGuid);
                        return;
                    }

                    if (IsTokenConstraint(constraintName))
                    {
                        continue;
                    }

                    throw;
                }*/
            }

            _logger.LogWarning("Failed to provision voucher for upsell order line {LineGuid} due to token collisions.", line.UpsellOrderLineGuid);
        }

        private async Task LoadExistingVoucherAsync(Guid upsellOrderLineGuid)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            _ = await context.UpsellVouchers.AsNoTracking()
                .FirstOrDefaultAsync(voucher => voucher.UpsellOrderLineGuid == upsellOrderLineGuid);
        }

        private async Task<Guid?> GetReservationGuidAsync(Guid upsellOrderGuid)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            return await context.UpsellOrderRecords.AsNoTracking()
                .Where(record => record.UpsellOrderGuid == upsellOrderGuid)
                .Select(record => record.ReservationGuid)
                .FirstOrDefaultAsync();
        }

        private static int? DetermineMaxUses(UpsellOrderLineRecord line, DateOnly validFrom, DateOnly validTo)
        {
            if (line.IsFreeUnlimitedUses || line.UnitPriceGross == 0)
            {
                return null;
            }

            return line.PricingModel switch
            {
                Integrations.RentoomApp.PartnersAndServices.Enums.PartnerServicePricingModel.PerPersonPerDay => ResolveNightCount(line, validFrom, validTo),
                Integrations.RentoomApp.PartnersAndServices.Enums.PartnerServicePricingModel.PerApartmentPerDay => ResolveNightCount(line, validFrom, validTo),
                Integrations.RentoomApp.PartnersAndServices.Enums.PartnerServicePricingModel.PerStay => 1,
                Integrations.RentoomApp.PartnersAndServices.Enums.PartnerServicePricingModel.OneTime => 1,
                _ => 1
            };
        }

        private static int ResolveNightCount(UpsellOrderLineRecord line, DateOnly validFrom, DateOnly validTo)
        {
            if (line.Nights > 0)
            {
                return line.Nights;
            }

            var nights = validTo.DayNumber - validFrom.DayNumber;
            return Math.Max(nights, 1);
        }

        private static bool IsUniqueViolation(DbUpdateException exception, out string? constraintName)
        {
            if (exception.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                constraintName = postgresException.ConstraintName;
                return true;
            }

            constraintName = null;
            return false;
        }

        private static bool IsOrderLineConstraint(string? constraintName)
        {
            return !string.IsNullOrWhiteSpace(constraintName)
                && constraintName.Contains("upsell_order_line_guid", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTokenConstraint(string? constraintName)
        {
            if (string.IsNullOrWhiteSpace(constraintName))
            {
                return false;
            }

            return constraintName.Contains("qr_token", StringComparison.OrdinalIgnoreCase)
                || constraintName.Contains("code_short", StringComparison.OrdinalIgnoreCase);
        }

      
    }
}
