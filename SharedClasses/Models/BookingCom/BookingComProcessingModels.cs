using System;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.BookingCom
{
    public class BookingComIncomingEmail
    {
        public string? MessageId { get; set; }
        public string? ReceivedDateTime { get; set; }
        public string? Subject { get; set; }
        public string? BodyHtml { get; set; }
        public string RawPayload { get; set; } = string.Empty;
    }

    public class BookingComLogStep
    {
        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
        public string Step { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }
    }

    public class BookingComReservationImportRequest
    {
        public Guid BookingComLogGuid { get; set; }
        public int ReservationId { get; set; }
        public BookingComIncomingEmail IncomingEmail { get; set; } = new();
        public string Provider { get; set; } = string.Empty;
        public string ProviderTransactionId { get; set; } = string.Empty;
    }

    public class BookingComReservationImportResult
    {
        public Guid BookingComLogGuid { get; set; }
        public Guid? ReservationGuid { get; set; }
        public int ReservationId { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Status { get; set; } = BookingComLogStatuses.Pending;
        public string Message { get; set; } = string.Empty;
    }

    public class BookingComEmailProcessingContext
    {
        public int? ReservationId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string ProviderTransactionId { get; set; } = string.Empty;
        public bool IsSynthetic { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    public class BookingComEmailProcessingResult
    {
        public Guid? BookingComLogGuid { get; set; }
        public Guid? ReservationGuid { get; set; }
        public int? ReservationId { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Status { get; set; } = BookingComLogStatuses.Pending;
        public string Message { get; set; } = string.Empty;
        public string? MessageId { get; set; }
    }

    public class BookingComBackfillRequest
    {
        public List<int> ReservationIds { get; set; } = new();
    }

    public class BookingComBackfillItemResult : BookingComEmailProcessingResult
    {
        public int RequestedReservationId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderTransactionId { get; set; } = string.Empty;
    }

    public class BookingComBackfillPreparedImport
    {
        public BookingComIncomingEmail IncomingEmail { get; set; } = new();
        public BookingComEmailProcessingContext ProcessingContext { get; set; } = new();
    }

    public static class BookingComLogStatuses
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Disabled = "Disabled";
        public const string Ignored = "Ignored";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Duplicate = "Duplicate";
    }
}
