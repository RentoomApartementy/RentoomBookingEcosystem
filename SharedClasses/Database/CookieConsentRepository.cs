using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RentoomBooking.SharedClasses.Models.Cookies;

namespace RentoomBooking.SharedClasses.Database
{
    public class CookieConsentRepository
    {
        private static readonly TimeSpan NoticeCacheTtl = TimeSpan.FromMinutes(30);
        private readonly PostgresBookingDbContext _context;
        private readonly IMemoryCache _memoryCache;

        public CookieConsentRepository(PostgresBookingDbContext context, IMemoryCache memoryCache)
        {
            _context = context;
            _memoryCache = memoryCache;
        }

        public async Task<CookieNoticeDto?> GetActiveNoticeAsync(string appCode, string? cultureName)
        {
            var normalizedAppCode = NormalizeAppCode(appCode);
            var normalizedCulture = NormalizeCulture(cultureName);
            var cacheKey = $"cookie-notice:{normalizedAppCode}:{normalizedCulture}";

            return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = NoticeCacheTtl;

                var now = DateTime.UtcNow;
                var source = await _context.CookieNoticeSources
                    .AsNoTracking()
                    .Where(x => x.AppCode == normalizedAppCode
                        && x.IsActive
                        && x.ValidFromUtc <= now
                        && (x.ValidToUtc == null || x.ValidToUtc >= now))
                    .Include(x => x.Translations)
                    .OrderByDescending(x => x.ValidFromUtc)
                    .ThenByDescending(x => x.Id)
                    .FirstOrDefaultAsync();

                return source is null ? null : BuildNoticeDto(source, normalizedCulture);
            });
        }

        public async Task<CookieNoticeDto?> GetNoticeBySourceAndVersionAsync(string appCode, int sourceId, string version, string? cultureName)
        {
            var normalizedAppCode = NormalizeAppCode(appCode);
            var normalizedCulture = NormalizeCulture(cultureName);
            var now = DateTime.UtcNow;

            var source = await _context.CookieNoticeSources
                .AsNoTracking()
                .Where(x => x.Id == sourceId
                    && x.AppCode == normalizedAppCode
                    && x.Version == version
                    && x.IsActive
                    && x.ValidFromUtc <= now
                    && (x.ValidToUtc == null || x.ValidToUtc >= now))
                .Include(x => x.Translations)
                .FirstOrDefaultAsync();

            return source is null ? null : BuildNoticeDto(source, normalizedCulture);
        }

        public async Task<CookieConsentAudit> AddConsentAuditAsync(CookieConsentAudit audit)
        {
            await _context.CookieConsentAudits.AddAsync(audit);
            await _context.SaveChangesAsync();
            return audit;
        }

        private static CookieNoticeDto? BuildNoticeDto(CookieNoticeSource source, string normalizedCulture)
        {
            var translation = SelectTranslation(source.Translations, normalizedCulture);
            if (translation is null)
            {
                return null;
            }

            return new CookieNoticeDto
            {
                SourceId = source.Id,
                TranslationId = translation.Id,
                AppCode = source.AppCode,
                Version = source.Version,
                Culture = translation.Culture,
                Title = translation.Title,
                BannerSummaryHtml = translation.BannerSummaryHtml,
                DetailsHtml = translation.DetailsHtml,
                AcceptLabel = translation.AcceptLabel,
                MoreLabel = translation.MoreLabel,
                LessLabel = translation.LessLabel,
                CloseLabel = translation.CloseLabel,
                ContentHash = BuildContentHash(source, translation)
            };
        }

        private static CookieNoticeTranslation? SelectTranslation(ICollection<CookieNoticeTranslation> translations, string normalizedCulture)
        {
            var neutralCulture = normalizedCulture.Split('-')[0];

            return translations.FirstOrDefault(t => string.Equals(t.Culture, normalizedCulture, StringComparison.OrdinalIgnoreCase))
                ?? translations.FirstOrDefault(t => string.Equals(t.Culture, neutralCulture, StringComparison.OrdinalIgnoreCase))
                ?? translations.FirstOrDefault(t => string.Equals(t.Culture, "pl-PL", StringComparison.OrdinalIgnoreCase))
                ?? translations.FirstOrDefault(t => string.Equals(t.Culture, "pl", StringComparison.OrdinalIgnoreCase))
                ?? translations.FirstOrDefault(t => string.Equals(t.Culture, "en-US", StringComparison.OrdinalIgnoreCase))
                ?? translations.FirstOrDefault(t => string.Equals(t.Culture, "en", StringComparison.OrdinalIgnoreCase))
                ?? translations.OrderBy(t => t.Id).FirstOrDefault();
        }

        private static string BuildContentHash(CookieNoticeSource source, CookieNoticeTranslation translation)
        {
            var payload = string.Join("|",
                source.AppCode,
                source.Version,
                translation.Culture,
                translation.Title,
                translation.BannerSummaryHtml,
                translation.DetailsHtml,
                translation.AcceptLabel,
                translation.MoreLabel,
                translation.LessLabel,
                translation.CloseLabel);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string NormalizeCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return "pl-PL";
            }

            try
            {
                return CultureInfo.GetCultureInfo(cultureName).Name;
            }
            catch (CultureNotFoundException)
            {
                return "pl-PL";
            }
        }

        private static string NormalizeAppCode(string appCode)
        {
            return string.IsNullOrWhiteSpace(appCode)
                ? string.Empty
                : appCode.Trim().ToLowerInvariant();
        }
    }
}
