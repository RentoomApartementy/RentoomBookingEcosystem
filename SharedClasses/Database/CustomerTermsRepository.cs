using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RentoomBooking.SharedClasses.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class CustomerTermsRepository
    {
        private static readonly TimeSpan TermsCacheTtl = TimeSpan.FromMinutes(30);
        private readonly IDbContextFactory<PostgresBookingDbContext> _contextFactory;
        private readonly IMemoryCache _memoryCache;

        public CustomerTermsRepository(IDbContextFactory<PostgresBookingDbContext> contextFactory, IMemoryCache memoryCache)
        {
            _contextFactory = contextFactory;
            _memoryCache = memoryCache;
        }

        public async Task<List<CustomerTermDisplayDto>> GetActiveTermsSourcesAsync(string? cultureName, bool onlyRequiredForWorkflow = false, CancellationToken ct = default)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            var cacheKey = BuildTermsCacheKey(normalizedCulture, onlyRequiredForWorkflow);
            var cachedTerms = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TermsCacheTtl;

                using var context = await _contextFactory.CreateDbContextAsync(ct);
                var query = context.CustomerTermsSources
                    .AsNoTracking()
                    .Where(t => t.IsActive);

                var sources = await query
                    .Include(t => t.Translations)
                    .OrderBy(t => t.SortOrder)
                    .ThenBy(t => t.Id)
                    .ToListAsync(ct);

                return sources.Select(source =>
                {
                    var translation = SelectTranslation(source.Translations, normalizedCulture, neutralCulture);

                    return new CustomerTermDisplayDto
                    {
                        Id = source.Id,
                        IsRequired = source.IsRequired,
                        Title = translation?.Title,
                        Description = translation?.Description ?? source.Description,
                        HtmlContent = translation?.HtmlContent,
                        Link = translation?.Link ?? source.Link,
                        TermsType = source.TermsType
                    };
                }).ToList();
            });

            return cachedTerms ?? [];
        }

        public async Task<CustomerTermDisplayDto?> GetTermByIdAsync(int id, string? cultureName, CancellationToken ct = default)
        {
            var terms = await GetActiveTermsSourcesAsync(cultureName, ct: ct);
            return terms.FirstOrDefault(t => t.Id == id);
        }

        public async Task AddAgreedTermsAsync(IEnumerable<CustomerAgreedTerms> agreedTerms)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.CustomerAgreedTerms.AddRangeAsync(agreedTerms);
            await context.SaveChangesAsync();
        }

        public async Task SaveAgreedTermsAsync(IEnumerable<CustomerAgreedTerms> agreedTerms)
        {
            var termsList = agreedTerms.ToList();
            if (termsList.Count == 0)
            {
                return;
            }

            using var context = await _contextFactory.CreateDbContextAsync();
            var reservationGuid = termsList[0].ReservationGuid;
            var termIds = termsList.Select(t => t.TermsSourceId).Distinct().ToList();

            var existingTerms = await context.CustomerAgreedTerms
                .Where(t => t.ReservationGuid == reservationGuid && termIds.Contains(t.TermsSourceId))
                .ToListAsync();

            foreach (var agreedTerm in termsList)
            {
                var existing = existingTerms.FirstOrDefault(t => t.TermsSourceId == agreedTerm.TermsSourceId);
                if (existing is null)
                {
                    await context.CustomerAgreedTerms.AddAsync(agreedTerm);
                    continue;
                }

                existing.IsAccepted = agreedTerm.IsAccepted;
                existing.AgreedAt = agreedTerm.AgreedAt;
                existing.ClientBitrixId = agreedTerm.ClientBitrixId;
            }

            await context.SaveChangesAsync();
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(Guid reservationGuid, string? cultureName = null)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            using var context = await _contextFactory.CreateDbContextAsync();
            var terms = await context.CustomerAgreedTerms
                .AsNoTracking()
                .Where(t => t.ReservationGuid == reservationGuid)
                .Include(t => t.TermsSource)
                .ThenInclude(t => t.Translations)
                .ToListAsync();

            return terms.Select(t =>
            {
                var translation = SelectTranslation(t.TermsSource.Translations, normalizedCulture, neutralCulture);

                return new CustomerAgreedTermDto
                {
                    TermsSourceId = t.TermsSourceId,
                    Description = translation?.Description ?? t.TermsSource.Description,
                    AgreedAt = t.AgreedAt,
                    IsRequired = t.TermsSource.IsRequired,
                    IsAccepted = t.IsAccepted,
                    TermsSourceType = t.TermsSource.TermsType
                };
            }).ToList();
        }

        public async Task<bool> UpdateAgreedTermAsync(Guid reservationGuid, int termsSourceId, bool isAccepted)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            var entity = await context.CustomerAgreedTerms
                .FirstOrDefaultAsync(t => t.ReservationGuid == reservationGuid && t.TermsSourceId == termsSourceId);

            if (entity is null)
            {
                return false;
            }

            entity.IsAccepted = isAccepted;
            entity.AgreedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            return true;
        }

        private static string NormalizeCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return "pl";
            }

            try
            {
                return CultureInfo.GetCultureInfo(cultureName).Name;
            }
            catch (CultureNotFoundException)
            {
                return "pl";
            }
        }

        private static CustomerTermsSourceTranslation? SelectTranslation(
            ICollection<CustomerTermsSourceTranslation> translations,
            string normalizedCulture,
            string neutralCulture)
        {
            return translations
                       .FirstOrDefault(t => string.Equals(t.Culture, normalizedCulture, System.StringComparison.OrdinalIgnoreCase))
                   ?? translations.FirstOrDefault(t => string.Equals(t.Culture, neutralCulture, System.StringComparison.OrdinalIgnoreCase))
                   ?? translations.FirstOrDefault(t => string.Equals(t.Culture, "pl", System.StringComparison.OrdinalIgnoreCase))
                   ?? translations.FirstOrDefault();
        }

        private static string BuildTermsCacheKey(string normalizedCulture, bool onlyRequiredForWorkflow)
        {
            return $"customer-terms:{normalizedCulture}:{onlyRequiredForWorkflow}";
        }
    }
}
