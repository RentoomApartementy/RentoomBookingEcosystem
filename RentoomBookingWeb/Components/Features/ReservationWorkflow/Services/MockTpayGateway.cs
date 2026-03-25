
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;

namespace RentoomBookingWeb.Components.Features.ReservationWorkflow.Services
{
    public class MockTpayGateway_2 : ITpayGateway
    {
        public Task<TpayTransactionResult> CreatePaymentAsync(Guid reservationGuid, Guid paymentSessionGuid, decimal amount, string currency, int? idobookiingid)
        {
            var transactionId = $"TPAY-{paymentSessionGuid:N}";
            var redirect = $"/tpay-mock/{paymentSessionGuid}?reservationGuid={reservationGuid}";

            var result = new TpayTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                RedirectUrl = redirect
            };

            return Task.FromResult(result);
        }

        public Task<TpayTransactionResult> GetPaymentStatusAsync(string transactionUid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TpayTransactionResult
            {
                Success = true,
                TransactionUid = transactionUid,
                TransactionStatus = "pending",
                AmountPaid = 0m
            });
        }
    }

}
