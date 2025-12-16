using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Tpay
{

    public interface ITpayGateway
    {
        Task<TpayTransactionResult> CreatePaymentAsync(Guid reservationGuid, Guid paymentSessionGuid, decimal amount, string currency);
    }

    public class MockTpayGateway : ITpayGateway
    {
        public Task<TpayTransactionResult> CreatePaymentAsync(Guid reservationGuid, Guid paymentSessionGuid, decimal amount, string currency)
        {
            var transactionId = $"TPAY-{paymentSessionGuid:N}";
            var redirect = $"/tpay-mock/{paymentSessionGuid}?reservationGuid={reservationGuid}";

            var result = new TpayTransactionResult
            {
                Success = true,
                TransactionId = transactionId,
                RedirectUrl = redirect,
                Message = "Mock payment created successfully.",
                RawResponse = "{Mock payment created successfully.}"
            };

            return Task.FromResult(result);
        }
    }
}
