using RentoomBooking.SharedClasses.Models.Payments;
using System;

namespace RentoomBooking.SharedClasses.Services.Payments
{
    public interface IPaymentOrchestrator
    {
        Task<PaymentSessionResponse> CreatePaymentAsync(PaymentIntentRequest intent, CancellationToken cancellationToken = default);
        Task<PaymentWebhookHandlingResult> HandleTpayWebhookAsync(string providerTransactionId, string status, CancellationToken cancellationToken = default);
    }

    public class PaymentOrchestrator : IPaymentOrchestrator
    {
        private readonly IReadOnlyDictionary<PaymentFlowType, IPaymentFlowHandler> _handlers;

        public PaymentOrchestrator(IEnumerable<IPaymentFlowHandler> handlers)
        {
            _handlers = handlers?.ToDictionary(h => h.FlowType) ?? throw new ArgumentNullException(nameof(handlers));
        }

        public async Task<PaymentSessionResponse> CreatePaymentAsync(PaymentIntentRequest intent, CancellationToken cancellationToken = default)
        {
            if (intent is null) throw new ArgumentNullException(nameof(intent));

            if (!_handlers.TryGetValue(intent.FlowType, out var handler))
            {
                throw new InvalidOperationException($"Unsupported payment flow type: {intent.FlowType}");
            }

            return await handler.CreatePaymentAsync(intent, cancellationToken);
        }

        public async Task<PaymentWebhookHandlingResult> HandleTpayWebhookAsync(string providerTransactionId, string status, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerTransactionId))
            {
                throw new ArgumentNullException(nameof(providerTransactionId));
            }

            foreach (var handler in _handlers.Values)
            {
                var handled = await handler.TryHandleWebhookAsync(providerTransactionId, status, cancellationToken);
                if (handled)
                {
                    return new PaymentWebhookHandlingResult
                    {
                        Handled = true,
                        Message = $"Handled by {handler.FlowType}"
                    };
                }
            }

            return new PaymentWebhookHandlingResult
            {
                Handled = false,
                Message = "No matching payment found."
            };
        }
    }
}
