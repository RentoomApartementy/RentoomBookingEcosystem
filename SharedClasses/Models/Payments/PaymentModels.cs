using System;

namespace RentoomBooking.SharedClasses.Models.Payments
{
    public enum PaymentFlowType
    {
        Reservation,
        Upsell
    }

    public class PaymentIntentRequest
    {
        public PaymentFlowType FlowType { get; set; } = PaymentFlowType.Reservation;
        public Guid? OrderId { get; set; }
        public string? SuccessUrl { get; set; }
        public string? ErrorUrl { get; set; }
        public Upsell.UpsellOrderRequest? UpsellOrder { get; set; }
        public string? NotificationUrl { get;  set; }
    }

    public class PaymentSessionResponse
    {
        public PaymentFlowType FlowType { get; set; }
        public Guid OrderId { get; set; }
        public Guid PaymentSessionGuid { get; set; }
        public string ProviderTransactionId { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }

    public class PaymentWebhookHandlingResult
    {
        public bool Handled { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
