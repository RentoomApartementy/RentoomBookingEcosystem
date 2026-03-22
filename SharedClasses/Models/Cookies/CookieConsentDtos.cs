namespace RentoomBooking.SharedClasses.Models.Cookies
{
    public static class CookieConsentAppCodes
    {
        public const string StayWell = "staywell";
        public const string RentoomBookingWeb = "rentoombookingweb";
    }

    public static class CookieConsentDecisions
    {
        public const string AcceptedAll = "accepted_all";
    }

    public class CookieNoticeDto
    {
        public int SourceId { get; set; }
        public int TranslationId { get; set; }
        public string AppCode { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Culture { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BannerSummaryHtml { get; set; } = string.Empty;
        public string DetailsHtml { get; set; } = string.Empty;
        public string AcceptLabel { get; set; } = string.Empty;
        public string MoreLabel { get; set; } = string.Empty;
        public string LessLabel { get; set; } = string.Empty;
        public string CloseLabel { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
    }

    public class SaveCookieConsentRequest
    {
        public int CookieNoticeSourceId { get; set; }
        public int CookieNoticeTranslationId { get; set; }
        public string CookieNoticeVersion { get; set; } = string.Empty;
        public string Decision { get; set; } = CookieConsentDecisions.AcceptedAll;
        public Guid ClientConsentId { get; set; }
        public string? Culture { get; set; }
        public string? RequestPath { get; set; }
        public string? Referrer { get; set; }
        public Guid? ReservationGuid { get; set; }
        public string? ContentHash { get; set; }
    }

    public class CookieConsentAuditResultDto
    {
        public int AuditId { get; set; }
        public DateTime AcceptedAtUtc { get; set; }
        public string ContentHash { get; set; } = string.Empty;
    }

    public class CookieConsentClientState
    {
        public Guid ClientConsentId { get; set; }
        public string Version { get; set; } = string.Empty;
        public int AuditId { get; set; }
        public DateTime AcceptedAtUtc { get; set; }
        public string ContentHash { get; set; } = string.Empty;
    }

    public class CookieConsentRequestMetadata
    {
        public string? IpAddress { get; set; }
        public string? AzureClientIp { get; set; }
        public string? ForwardedForRaw { get; set; }
        public string? UserAgent { get; set; }
        public string? RequestPath { get; set; }
        public string? Referrer { get; set; }
    }
}
