namespace RentoomBooking.SharedClasses.Models.IdoBooking.Payments
{
    public class PaymentAddRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public PaymentAddParams Params { get; set; } = new();
    }

    public class PaymentAddParams
    {
        public List<PaymentAdd> Payments { get; set; } = new();
    }

    public class PaymentAdd
    {
        public int ReservationId { get; set; }
        public float Value { get; set; }
        public string Type { get; set; } = PaymentType.Payment;
        public string PaymentSystem { get; set; } = PaymentSystemType.Transfer;
        public string? Currency { get; set; }
        public string? ExternalPaymentId { get; set; }
    }

    public class PaymentAddResponseType
    {
        public PaymentAddResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class PaymentAddResponse
    {
        public GateErrorType? Errors { get; set; }
        public List<PaymentAddResult>? Results { get; set; }
    }

    public class PaymentAddResult
    {
        public string? Id { get; set; }
        public string? ExternalPaymentId { get; set; }
        public int ReservationId { get; set; }
        public GateErrorType? Error { get; set; }
    }

    public class PaymentActionRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public PaymentActionParams Params { get; set; } = new();
    }

    public class PaymentActionParams
    {
        public List<PaymentIdentifier> Payments { get; set; } = new();
    }

    public class PaymentIdentifier
    {
        public int Id { get; set; }
    }

    public class PaymentActionResponseType
    {
        public PaymentActionResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class PaymentActionResponse
    {
        public GateErrorType? Errors { get; set; }
        public List<PaymentActionResult>? Results { get; set; }
    }

    public class PaymentActionResult
    {
        public string? Id { get; set; }
        public GateErrorType? Error { get; set; }
    }

    public class PaymentEditRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public PaymentEditParams Params { get; set; } = new();
    }

    public class PaymentEditParams
    {
        public List<PaymentEdit> Payments { get; set; } = new();
    }

    public class PaymentEdit
    {
        public int Id { get; set; }
        public float? Value { get; set; }
        public string? PaymentSystem { get; set; }
        public string? Currency { get; set; }
    }

    public class PaymentEditResponseType
    {
        public PaymentActionResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class PaymentFormsRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
    }

    public class PaymentFormsResponseType
    {
        public PaymentFormsResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class PaymentFormsResponse
    {
        public GateErrorType? Errors { get; set; }
        public List<PaymentForm>? Results { get; set; }
    }

    public class PaymentForm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PaymentGetRequest
    {
        public AuthenticateType Authenticate { get; set; } = new();
        public PaymentGetSettings? Settings { get; set; }
        public PaymentGetParams? Params { get; set; }
    }

    public class PaymentGetSettings
    {
        public int? Page { get; set; } = 1;
        public int? Number { get; set; } = 100;
    }

    public class PaymentGetParams
    {
        public List<int>? ReservationIds { get; set; }
        public List<int>? PaymentIds { get; set; }
        public List<string>? Statuses { get; set; }
        public List<string>? PaymentTypes { get; set; }
    }

    public class PaymentGetResponseType
    {
        public PaymentGetResponse? Result { get; set; }
        public string? Id { get; set; }
    }

    public class PaymentGetResponse
    {
        public GateErrorType? Errors { get; set; }
        public List<PaymentDetails>? Results { get; set; }
        public int? Page { get; set; }
        public int? CountOnPage { get; set; }
        public int? PageAll { get; set; }
        public int? CountAll { get; set; }
    }

    public class PaymentDetails
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public float Value { get; set; }
        public string Type { get; set; } = PaymentType.Payment;
        public string Status { get; set; } = PaymentStatus.Pending;
        public string PaymentSystem { get; set; } = string.Empty;
        public string? ExternalPaymentId { get; set; }
        public string? AccountingDate { get; set; }
        public string? Currency { get; set; }
    }

    public static class PaymentType
    {
        public const string Payment = "payment";
        public const string Advance = "advance";
        public const string Repayment = "repayment";
    }

    public static class PaymentSystemType
    {
        public const string Transfer = "transfer";
        public const string Cash = "cash";
    }

    public static class PaymentStatus
    {
        public const string Pending = "pending";
        public const string Processed = "processed";
        public const string Cancelled = "cancelled";
    }
}