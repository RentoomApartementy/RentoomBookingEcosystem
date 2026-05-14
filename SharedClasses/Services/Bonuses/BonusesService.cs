using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;

namespace RentoomBooking.SharedClasses.Services.Bonuses
{
    public sealed class BonusCalculationRequest
    {
        public string? BonusInputName { get; set; }
        public BookingChannel BookingChannel { get; set; } = BookingChannel.WebDirect;
        public DateOnly ReservationStartDate { get; set; }
        public int ReservationDays { get; set; }
        public int ApartmentItemId { get; set; }
        public decimal OfferPrice { get; set; }
        public decimal MandatoryAddonsTotalPrice { get; set; }
        public decimal TotalCostGrossAmount { get; set; }
    }

    public sealed class BonusCalculationResult
    {
        public bool IsApplied { get; set; }
        public string NormalizedBonusInputName { get; set; } = string.Empty;
        public string? RejectReason { get; set; }

        public int? AppliedBonusId { get; set; }
        public string? AppliedBonusName { get; set; }
        public BonusDiscountValueType? AppliedBonusValueType { get; set; }
        public decimal AppliedBonusValue { get; set; }

        public decimal BonusBasePln { get; set; }
        public decimal DiscountAmountPln { get; set; }
    }

    public interface IBonusesService
    {
        Task<BonusCalculationResult> EvaluateAsync(BonusCalculationRequest request, CancellationToken cancellationToken = default);
    }

    public class BonusesService : IBonusesService
    {
        private readonly IDbContextFactory<RappPartnersDBContext> _dbContextFactory;

        public BonusesService(IDbContextFactory<RappPartnersDBContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<BonusCalculationResult> EvaluateAsync(BonusCalculationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedBonusInput = request.BonusInputName?.Trim() ?? string.Empty;
            var bonusBase = Math.Max(0m, request.OfferPrice - request.MandatoryAddonsTotalPrice);

            var result = new BonusCalculationResult
            {
                IsApplied = false,
                NormalizedBonusInputName = normalizedBonusInput,
                BonusBasePln = bonusBase,
                DiscountAmountPln = 0m
            };

            if (string.IsNullOrWhiteSpace(normalizedBonusInput))
            {
                result.RejectReason = "empty";
                return result;
            }

            if (request.BookingChannel != BookingChannel.WebDirect)
            {
                result.RejectReason = "channel_not_supported";
                return result;
            }

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var upperBonusName = normalizedBonusInput.ToUpperInvariant();
            var bonus = await dbContext.BonusDiscounts
                .AsNoTracking()
                .Include(item => item.Targets)
                .FirstOrDefaultAsync(item => item.Name.ToUpper() == upperBonusName, cancellationToken);

            if (bonus is null)
            {
                result.RejectReason = "not_found";
                return result;
            }

            if (bonus.ManualStatus != BonusDiscountManualStatus.Enabled)
            {
                result.RejectReason = "disabled";
                return result;
            }

            if (bonus.Targets == null ||  (bonus.Targets.Count > 0 && !bonus.Targets.Any(target => target.ApartmentItemId == request.ApartmentItemId)) )
            {
                result.RejectReason = "invalid_target";
                return result;
            }

            var validFrom = DateOnly.FromDateTime(bonus.ValidFrom);
            var validTo = bonus.ValidTo.HasValue ? DateOnly.FromDateTime(bonus.ValidTo.Value) : (DateOnly?)null;

            if (request.ReservationStartDate < validFrom || (validTo.HasValue && request.ReservationStartDate > validTo.Value))
            {
                result.RejectReason = "outside_reservation_dates";
                return result;
            }

            if (bonus.MinimumReservationDays.HasValue && request.ReservationDays < bonus.MinimumReservationDays.Value)
            {
                result.RejectReason = "below_minimum_reservation_days";
                return result;
            }

            if (bonus.MinimumOrderGrossAmount.HasValue && request.TotalCostGrossAmount < bonus.MinimumOrderGrossAmount.Value)
            {
                result.RejectReason = "below_minimum_order_gross_amount";
                return result;
            }

            var discountAmount = bonus.ValueType switch
            {
                BonusDiscountValueType.Percent => Math.Round(bonusBase * (bonus.Value / 100m), 2, MidpointRounding.AwayFromZero),
                BonusDiscountValueType.FixedAmount => bonus.Value,
                _ => 0m
            };

            if (discountAmount < 0m)
            {
                discountAmount = 0m;
            }

            if (discountAmount > bonusBase)
            {
                discountAmount = bonusBase;
            }

            if (discountAmount <= 0m)
            {
                result.RejectReason = "no_discount";
                return result;
            }

            result.IsApplied = true;
            result.AppliedBonusId = bonus.Id;
            result.AppliedBonusName = bonus.Name;
            result.AppliedBonusValueType = bonus.ValueType;
            result.AppliedBonusValue = bonus.Value;
            result.DiscountAmountPln = discountAmount;
            result.RejectReason = null;

            return result;
        }
    }
}
