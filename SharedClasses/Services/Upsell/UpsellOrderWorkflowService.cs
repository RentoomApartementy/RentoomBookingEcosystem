using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Globalization;
using System.Linq;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellOrderWorkflowService
    {
        Task<UpsellOrderRecord> CreateOrderAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default);
        Task<UpsellPaymentInitResult> InitiatePaymentAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default);
        Task<UpsellPaymentInitResult> CreateOrderAndInitiatePaymentAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default);
        Task<UpsellOrderRecord> CreatePaidOrderAsync(UpsellOrderRequest request, IReadOnlyList<UpsellOrderLineRecord> lines, string providerTransactionId, string? provider, DateTime paidAtUtc, CancellationToken cancellationToken = default);
        Task HandleTpayWebhookAsync(UpsellWebhookDto dto, CancellationToken cancellationToken = default);
    }

    public class UpsellOrderWorkflowService : IUpsellOrderWorkflowService
    {
        private readonly IUpsellOrderStore _store;
        private readonly IUpsellCatalogService _catalogService;
        private readonly IUpsellVoucherProvisioningService _voucherProvisioningService;
        private readonly ITpayClient _tpayClient;
        private readonly TpaySettings _tpaySettings;
        private readonly ILogger<UpsellOrderWorkflowService> _logger;

        public UpsellOrderWorkflowService(
            IUpsellOrderStore store,
            IUpsellCatalogService catalogService,
            IUpsellVoucherProvisioningService voucherProvisioningService,
            ITpayClient tpayClient,
            IOptions<TpaySettings> tpayOptions,
            ILogger<UpsellOrderWorkflowService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _voucherProvisioningService = voucherProvisioningService ?? throw new ArgumentNullException(nameof(voucherProvisioningService));
            _tpayClient = tpayClient ?? throw new ArgumentNullException(nameof(tpayClient));
            _tpaySettings = tpayOptions?.Value ?? throw new ArgumentNullException(nameof(tpayOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<UpsellOrderRecord> CreateOrderAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            return _store.CreateAsync(request, cancellationToken);
        }

        public async Task<UpsellPaymentInitResult> CreateOrderAndInitiatePaymentAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default)
        {
            var record = await CreateOrderAsync(request, cancellationToken);
            return await InitiatePaymentAsync(record.UpsellOrderGuid, cancellationToken);
        }

        public async Task<UpsellOrderRecord> CreatePaidOrderAsync(UpsellOrderRequest request, IReadOnlyList<UpsellOrderLineRecord> lines, string providerTransactionId, string? provider, DateTime paidAtUtc, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (lines is null) throw new ArgumentNullException(nameof(lines));

            var record = await _store.CreateWithLinesAsync(request, lines, cancellationToken);
            record.PaymentStatus = PaymentStatuses.Paid;
            record.ProviderTransactionId = providerTransactionId;
            record.Provider = string.IsNullOrWhiteSpace(provider) ? record.Provider : provider;
            record.PaidAtUtc = paidAtUtc;
            record.State.GrandTotal = lines.Sum(line => line.LineTotalGross);
            record.State.UpsellsTotal = record.State.GrandTotal;
            foreach (var line in lines)
            {
                line.LineStatus = UpsellLineStatuses.Paid;
            }

            await _store.UpdateAsync(record, cancellationToken);
            await _store.ReplaceLinesAsync(record.UpsellOrderGuid, lines, cancellationToken);
            try
            {
                await _voucherProvisioningService.EnsureForOrderAsync(record.UpsellOrderGuid);
                _logger.LogInformation("Ensured upsell vouchers for paid order {OrderGuid}.", record.UpsellOrderGuid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure upsell vouchers for paid order {OrderGuid}.", record.UpsellOrderGuid);
                throw;
            }
            return record;
        }

        public async Task<UpsellPaymentInitResult> InitiatePaymentAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var record = await RequireOrderAsync(upsellOrderGuid, cancellationToken);

                if (record.PaymentStatus == PaymentStatuses.Paid && record.PaymentSessionGuid.HasValue)
                {
                    return BuildInitResult(record);
                }

                if (record.PaymentStatus == PaymentStatuses.Initiated && record.PaymentSessionGuid.HasValue)
                {
                    return BuildInitResult(record);
                }

                var paymentSessionGuid = Guid.NewGuid();
                var summary = await BuildSummaryAsync(record, cancellationToken);
                var request = record.State.Request ?? throw new InvalidOperationException("Upsell order request is missing.");

                var successUrl = string.IsNullOrWhiteSpace(request.SuccessUrl) ? _tpaySettings.SuccessUrl : request.SuccessUrl;
                var errorUrl = string.IsNullOrWhiteSpace(request.ErrorUrl) ? _tpaySettings.ErrorUrl : request.ErrorUrl;
                var notificationUrl = string.IsNullOrWhiteSpace(request.NotificationUrl) ? _tpaySettings.NotificationUrl : request.NotificationUrl;

                var tpayRequest = new TpayTransactionRequest
                {
                    Amount = summary.GrandTotal,
                    Currency = string.IsNullOrWhiteSpace(request.Currency) ? _tpaySettings.DefaultCurrency : request.Currency,
                    Description = request.ReservationGuid.HasValue
                        ? $"Upsell order {record.UpsellOrderGuid} (reservation {request.ReservationGuid})"
                        : $"Upsell order {record.UpsellOrderGuid}",
                    Payer = new TpayPayer
                    {
                        Email = request.Buyer.Email,
                        Name = string.IsNullOrWhiteSpace(request.Buyer.Name) ? request.Buyer.Email : request.Buyer.Name,
                        Phone = request.Buyer.Phone
                    },
                    SuccessUrl = successUrl,
                    ErrorUrl = errorUrl,
                    NotificationUrl = notificationUrl,
                    HiddenDescription = record.UpsellOrderGuid.ToString()
                };

                if (string.IsNullOrWhiteSpace(tpayRequest.Payer.Email))
                {
                    throw new InvalidOperationException("Payer email is required for upsell payment.");
                }

                if (string.IsNullOrWhiteSpace(tpayRequest.NotificationUrl))
                {
                    throw new InvalidOperationException("Tpay notification URL is not configured.");
                }

                var tpayResult = await _tpayClient.CreateTransactionAsync(tpayRequest, cancellationToken);
                if (!tpayResult.Success || string.IsNullOrWhiteSpace(tpayResult.TransactionId) || string.IsNullOrWhiteSpace(tpayResult.RedirectUrl))
                {
                    throw new InvalidOperationException(tpayResult.Message ?? "Failed to create upsell payment.");
                }

                record.PaymentSessionGuid = paymentSessionGuid;
                record.PaymentStatus = PaymentStatuses.Initiated;
                record.Provider = record.Provider ?? "TPAY";
                record.ProviderTransactionId = tpayResult.TransactionId;
                record.State.PaymentRedirectUrl = tpayResult.RedirectUrl;
                record.State.UpsellsTotal = summary.UpsellsTotal;
                record.State.GrandTotal = summary.GrandTotal;
                record.Lines = summary.Lines;

                try
                {
                    await _store.UpdateAsync(record, cancellationToken);
                    await _store.ReplaceLinesAsync(record.UpsellOrderGuid, summary.Lines, cancellationToken);
                    return BuildInitResult(record);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while initiating upsell payment for {OrderGuid}. Retrying.", upsellOrderGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                }
            }
        }

        public async Task HandleTpayWebhookAsync(UpsellWebhookDto dto, CancellationToken cancellationToken = default)
        {
            if (dto is null) throw new ArgumentNullException(nameof(dto));

            while (true)
            {
                var record = await RequireOrderAsync(dto.UpsellOrderGuid, cancellationToken);

                if (record.PaymentSessionGuid != dto.PaymentSessionGuid)
                {
                    _logger.LogWarning("Payment session guid mismatch for upsell order {OrderGuid}.", dto.UpsellOrderGuid);
                    throw new InvalidOperationException("Payment session mismatch.");
                }

                if (!string.Equals(record.ProviderTransactionId, dto.ProviderTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Provider transaction id mismatch for upsell order {OrderGuid}.", dto.UpsellOrderGuid);
                    throw new InvalidOperationException("Transaction mismatch.");
                }

                if (string.IsNullOrWhiteSpace(record.PaymentStatus) || record.PaymentStatus == PaymentStatuses.None)
                {
                    _logger.LogWarning("Received webhook for upsell order {OrderGuid} without initiated payment.", dto.UpsellOrderGuid);
                    throw new InvalidOperationException("Payment not initiated.");
                }

                if (record.PaymentStatus == PaymentStatuses.Paid)
                {
                    return;
                }

                var isPaid = string.Equals(dto.Status, "PAID", StringComparison.OrdinalIgnoreCase);
                record.PaymentStatus = isPaid ? PaymentStatuses.Paid : PaymentStatuses.Failed;
                record.PaidAtUtc = isPaid ? DateTime.UtcNow : record.PaidAtUtc;

                try
                {
                    await _store.UpdateAsync(record, cancellationToken);
                    if (isPaid)
                    {
                        var lines = await _store.GetLinesAsync(record.UpsellOrderGuid, cancellationToken);
                        foreach (var line in lines)
                        {
                            line.LineStatus = UpsellLineStatuses.Paid;
                        }
                        await _store.ReplaceLinesAsync(record.UpsellOrderGuid, lines, cancellationToken);
                        try
                        {
                            await _voucherProvisioningService.EnsureForOrderAsync(record.UpsellOrderGuid);
                            _logger.LogInformation("Ensured upsell vouchers for paid order {OrderGuid}.", record.UpsellOrderGuid);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to ensure upsell vouchers for paid order {OrderGuid}.", record.UpsellOrderGuid);
                            throw;
                        }
                    }
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while handling upsell webhook for {OrderGuid}. Retrying.", dto.UpsellOrderGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                }
            }
        }

        private async Task<UpsellOrderSummary> BuildSummaryAsync(UpsellOrderRecord record, CancellationToken cancellationToken)
        {
            var request = record.State.Request ?? throw new InvalidOperationException("Upsell order request is missing.");
            var culture = CultureInfo.CurrentUICulture.Name;
            var tiles = await _catalogService.GetUpsellTilesForApartmentAsync(request.ApartmentId, culture, "staywell", cancellationToken);
            var tileLookup = tiles.ToDictionary(tile => tile.PartnerServiceId);

            var pricingContext = new ReservationPricingContext
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Adults = request.Adults,
                Children = request.Children,
                Currency = request.Currency
            };

            var lines = new List<UpsellOrderLineRecord>();
            var total = 0m;

            foreach (var selected in request.SelectedUpsells)
            {
                if (!tileLookup.TryGetValue(selected.PartnerServiceId, out var tile))
                {
                    continue;
                }

                var quantity = Math.Max(1, selected.Quantity);
                var lineTotal = UpsellPricingCalculator.CalculateTotal(
                    tile.PricingModel,
                    tile.Price,
                    pricingContext.Nights,
                    pricingContext.TotalGuests,
                    quantity);

                lines.Add(new UpsellOrderLineRecord
                {
                    UpsellOrderGuid = record.UpsellOrderGuid,
                    PartnerServiceId = tile.PartnerServiceId,
                    TitleSnapshot = tile.Title,
                    PricingModel = tile.PricingModel,
                    Quantity = quantity,
                    UnitPriceGross = tile.Price,
                    Nights = pricingContext.Nights,
                    TotalGuests = pricingContext.TotalGuests,
                    LineTotalGross = lineTotal,
                    Currency = request.Currency,
                    LineStatus = UpsellLineStatuses.Pending,
                    UpsellDefinitionSnapshot = tile
                });

                total += lineTotal;
            }

            record.State.UpsellsTotal = total;
            record.State.GrandTotal = total;

            return new UpsellOrderSummary
            {
                Lines = lines,
                UpsellsTotal = total,
                GrandTotal = total
            };
        }

        private UpsellPaymentInitResult BuildInitResult(UpsellOrderRecord record)
        {
            return new UpsellPaymentInitResult
            {
                UpsellOrderGuid = record.UpsellOrderGuid,
                PaymentSessionGuid = record.PaymentSessionGuid ?? Guid.Empty,
                ProviderTransactionId = record.ProviderTransactionId ?? string.Empty,
                RedirectUrl = record.State.PaymentRedirectUrl ?? string.Empty,
                Provider = record.Provider ?? "TPAY"
            };
        }

        private async Task<UpsellOrderRecord> RequireOrderAsync(Guid upsellOrderGuid, CancellationToken cancellationToken)
        {
            var record = await _store.GetAsync(upsellOrderGuid, cancellationToken);
            return record ?? throw new InvalidOperationException($"Upsell order {upsellOrderGuid} not found.");
        }

        private sealed class UpsellOrderSummary
        {
            public List<UpsellOrderLineRecord> Lines { get; init; } = new();
            public decimal UpsellsTotal { get; init; }
            public decimal GrandTotal { get; init; }
        }
    }
}
