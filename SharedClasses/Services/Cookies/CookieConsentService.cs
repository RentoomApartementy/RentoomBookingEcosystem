using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Cookies;

namespace RentoomBooking.SharedClasses.Services.Cookies
{
    public class CookieConsentService
    {
        private readonly CookieConsentRepository _repository;

        public CookieConsentService(CookieConsentRepository repository)
        {
            _repository = repository;
        }

        public Task<CookieNoticeDto?> GetActiveNoticeAsync(string appCode, string? cultureName)
        {
            return _repository.GetActiveNoticeAsync(appCode, cultureName);
        }

        public async Task<CookieConsentAuditResultDto?> SaveConsentAsync(
            string appCode,
            SaveCookieConsentRequest request,
            CookieConsentRequestMetadata metadata)
        {
            if (request.CookieNoticeSourceId <= 0
                || request.CookieNoticeTranslationId <= 0
                || request.ClientConsentId == Guid.Empty
                || string.IsNullOrWhiteSpace(request.CookieNoticeVersion)
                || !string.Equals(request.Decision, CookieConsentDecisions.AcceptedAll, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var notice = await _repository.GetNoticeBySourceAndVersionAsync(
                appCode,
                request.CookieNoticeSourceId,
                request.CookieNoticeVersion,
                request.Culture);

            if (notice is null || notice.TranslationId != request.CookieNoticeTranslationId)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.ContentHash)
                || !string.Equals(request.ContentHash, notice.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var audit = new CookieConsentAudit
            {
                AppCode = notice.AppCode,
                CookieNoticeSourceId = notice.SourceId,
                CookieNoticeVersion = notice.Version,
                CookieNoticeTranslationId = notice.TranslationId,
                Decision = CookieConsentDecisions.AcceptedAll,
                ClientConsentId = request.ClientConsentId,
                AcceptedAtUtc = DateTime.UtcNow,
                Culture = notice.Culture,
                ContentHash = notice.ContentHash,
                IpAddress = metadata.IpAddress,
                AzureClientIp = metadata.AzureClientIp,
                ForwardedForRaw = metadata.ForwardedForRaw,
                UserAgent = metadata.UserAgent,
                RequestPath = request.RequestPath ?? metadata.RequestPath,
                Referrer = request.Referrer ?? metadata.Referrer,
                ReservationGuid = request.ReservationGuid
            };

            var savedAudit = await _repository.AddConsentAuditAsync(audit);

            return new CookieConsentAuditResultDto
            {
                AuditId = savedAudit.Id,
                AcceptedAtUtc = savedAudit.AcceptedAtUtc,
                ContentHash = notice.ContentHash
            };
        }
    }
}
