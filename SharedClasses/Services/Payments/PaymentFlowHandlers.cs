using RentoomBooking.SharedClasses.Models.Payments;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;

namespace RentoomBooking.SharedClasses.Services.Payments
{
    
    public interface IPaymentFlowHandler
    {
        PaymentFlowType FlowType { get; }
        Task<PaymentSessionResponse> CreatePaymentAsync(PaymentIntentRequest intent, CancellationToken cancellationToken = default);
        Task<bool> TryHandleWebhookAsync(string providerTransactionId, string status, CancellationToken cancellationToken = default);
    }

    //Handler Service dla rezerwacji - flow Tpay dla zakladania rezerwacji w rentoombooking, gdzie p?atno?? jest inicjowana przed finalizacj? rezerwacji w rentoombooking!
    //TODO: do przemyslenia jak ogarnac ze staywell czy bedzie korzystac z tego flow przy przedluzaniu rezerwacji, czy bedzie potrzebny osobny flow, czy moze wystarczy przekazanie innego parametru do tego flowa przy przedluzaniu rezerwacji
    public class ReservationPaymentFlowHandler : IPaymentFlowHandler
    {
        private readonly IReservationWorkflowService _workflowService;
        private readonly IReservationStore _reservationStore;

        public ReservationPaymentFlowHandler(IReservationWorkflowService workflowService, IReservationStore reservationStore)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _reservationStore = reservationStore ?? throw new ArgumentNullException(nameof(reservationStore));
        }

        public PaymentFlowType FlowType => PaymentFlowType.Reservation;

        public async Task<PaymentSessionResponse> CreatePaymentAsync(PaymentIntentRequest intent, CancellationToken cancellationToken = default)
        {
            if (intent.OrderId is null || intent.OrderId == Guid.Empty)
            {
                throw new InvalidOperationException("Reservation payment requires OrderId.");
            }

            var result = await _workflowService.InitiatePaymentAsync(intent.OrderId.Value);
            return new PaymentSessionResponse
            {
                FlowType = FlowType,
                OrderId = result.ReservationGuid,
                PaymentSessionGuid = result.PaymentSessionGuid,
                ProviderTransactionId = result.ProviderTransactionId,
                RedirectUrl = result.RedirectUrl,
                Provider = result.Provider
            };
        }

        public async Task<bool> TryHandleWebhookAsync(string providerTransactionId, string status, CancellationToken cancellationToken = default)
        {
            var record = await _reservationStore.GetByProviderTransactionIdAsync(providerTransactionId, cancellationToken);
            if (record is null || !record.PaymentSessionGuid.HasValue)
            {
                return false;
            }

            var dto = new TpayWebhookDto
            {
                ReservationGuid = record.ReservationGuid,
                PaymentSessionGuid = record.PaymentSessionGuid.Value,
                ProviderTransactionId = providerTransactionId,
                Status = status,
                Signature = "validated"
            };

            await _workflowService.HandleTpayWebhookAsync(dto);
            return true;
        }
    }

    //Handler Service dla upsell - flow Tpay upsellowy, gdzie p?atno?? jest inicjowana przez Staywell
    public class UpsellPaymentFlowHandler : IPaymentFlowHandler
    {
        private readonly IUpsellOrderWorkflowService _workflowService;
        private readonly IUpsellOrderStore _store;

        public UpsellPaymentFlowHandler(IUpsellOrderWorkflowService workflowService, IUpsellOrderStore store)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public PaymentFlowType FlowType => PaymentFlowType.Upsell;

        public async Task<PaymentSessionResponse> CreatePaymentAsync(PaymentIntentRequest intent, CancellationToken cancellationToken = default)
        {
            UpsellPaymentInitResult result;

            if (intent.OrderId.HasValue && intent.OrderId.Value != Guid.Empty)
            {
                result = await _workflowService.InitiatePaymentAsync(intent.OrderId.Value, cancellationToken);
            }
            else
            {
                if (intent.UpsellOrder is null)
                {
                    throw new InvalidOperationException("Upsell payment requires UpsellOrder details.");
                }

                if (string.IsNullOrWhiteSpace(intent.UpsellOrder.SuccessUrl) && !string.IsNullOrWhiteSpace(intent.SuccessUrl))
                {
                    intent.UpsellOrder.SuccessUrl = intent.SuccessUrl;
                }

                if (string.IsNullOrWhiteSpace(intent.UpsellOrder.ErrorUrl) && !string.IsNullOrWhiteSpace(intent.ErrorUrl))
                {
                    intent.UpsellOrder.ErrorUrl = intent.ErrorUrl;
                }

                if (string.IsNullOrWhiteSpace(intent.UpsellOrder.NotificationUrl) && !string.IsNullOrWhiteSpace(intent.NotificationUrl))
                {
                    intent.UpsellOrder.NotificationUrl = intent.NotificationUrl;
                }


                result = await _workflowService.CreateOrderAndInitiatePaymentAsync(intent.UpsellOrder, cancellationToken);
            }

            return new PaymentSessionResponse
            {
                FlowType = FlowType,
                OrderId = result.UpsellOrderGuid,
                PaymentSessionGuid = result.PaymentSessionGuid,
                ProviderTransactionId = result.ProviderTransactionId,
                RedirectUrl = result.RedirectUrl,
                Provider = result.Provider
            };
        }

        public async Task<bool> TryHandleWebhookAsync(string providerTransactionId, string status, CancellationToken cancellationToken = default)
        {
            var record = await _store.GetByProviderTransactionIdAsync(providerTransactionId, cancellationToken);
            if (record is null || !record.PaymentSessionGuid.HasValue)
            {
                return false;
            }

            var dto = new UpsellWebhookDto
            {
                UpsellOrderGuid = record.UpsellOrderGuid,
                PaymentSessionGuid = record.PaymentSessionGuid.Value,
                ProviderTransactionId = providerTransactionId,
                Status = status
            };

            await _workflowService.HandleTpayWebhookAsync(dto, cancellationToken);
            return true;
        }
    }
}
