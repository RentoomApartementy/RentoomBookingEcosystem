using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Tpay
{
    public class TpayOpenApiGateway : ITpayGateway
    {
        private readonly ITpayClient _client;
        private readonly IReservationStore _store;
        private readonly TpaySettings _settings;
        private readonly ILogger<TpayOpenApiGateway> _logger;

        public TpayOpenApiGateway(
            ITpayClient client,
            IReservationStore store,
            IOptions<TpaySettings> options,
            ILogger<TpayOpenApiGateway> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TpayTransactionResult> CreatePaymentAsync(Guid reservationGuid, Guid paymentSessionGuid, decimal amount, string currency)
        {
            var record = await _store.GetAsync(reservationGuid)
                ?? throw new InvalidOperationException($"Reservation {reservationGuid} not found.");

            var payerEmail = record.State.Client?.Email;
            var payerName = string.Join(" ", new[] { record.State.Client?.FirstName, record.State.Client?.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var request = new TpayTransactionRequest
            {
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(currency) ? _settings.DefaultCurrency : currency,
                Description = $"Reservation {reservationGuid}",


                Payer = new TpayPayer
                {
                    Email = payerEmail ?? string.Empty,
                    Name = string.IsNullOrWhiteSpace(payerName) ? record.State.Client?.Email ?? "Payer" : payerName
                    // Phone = record.State.Client?.Phone ?? string.Empty

                },
                SuccessUrl = _settings.SuccessUrl,
                ErrorUrl = _settings.ErrorUrl,
                NotificationUrl = _settings.NotificationUrl,
                HiddenDescription = reservationGuid.ToString(),
            };

            if (string.IsNullOrWhiteSpace(request.Payer.Email))
            {
                throw new InvalidOperationException("Payer email is required to create Tpay transaction.");
            }

            if (string.IsNullOrWhiteSpace(request.NotificationUrl))
            {
                throw new InvalidOperationException("Tpay notification URL is not configured.");
            }

            _logger.LogInformation("Creating Tpay transaction for reservation {ReservationGuid}", reservationGuid);
            var result = await _client.CreateTransactionAsync(request);
            if (!result.Success)
            {
                _logger.LogWarning("Failed to create Tpay payment for {ReservationGuid}: {Message}", reservationGuid, result.Message);
            }
            else
            {
                _logger.LogInformation("Tpay transaction created for {ReservationGuid} with transactionId {TransactionId}", reservationGuid, result.TransactionId);
            }

            return result;
        }
    }
}
