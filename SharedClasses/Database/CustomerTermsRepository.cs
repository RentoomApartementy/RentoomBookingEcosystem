using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class CustomerTermsRepository
    {
        private readonly PostgresBookingDbContext _context;

        public CustomerTermsRepository(PostgresBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<CustomerTermDisplayDto>> GetActiveTermsSourcesAsync(string? cultureName, bool onlyRequiredForWorkflow = false)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            var query = _context.CustomerTermsSources
                .Where(t => t.IsActive);//.ToListAsync();

            //if (onlyRequiredForWorkflow)
            //{
             //   query = query.Where(t => t.IsRequiredForReservationWorkflow);
           // }

            var sources = await query
                .Include(t => t.Translations)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

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
        }

        public async Task<CustomerTermDisplayDto?> GetTermByIdAsync(int id, string? cultureName)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            var source = await _context.CustomerTermsSources
                .Include(t => t.Translations)
                .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

            if (source == null) return null;

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
        }

        public async Task AddAgreedTermsAsync(IEnumerable<CustomerAgreedTerms> agreedTerms)
        {
            await _context.CustomerAgreedTerms.AddRangeAsync(agreedTerms);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(Guid reservationGuid, string? cultureName = null)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            var terms = await _context.CustomerAgreedTerms
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
            var entity = await _context.CustomerAgreedTerms
                .FirstOrDefaultAsync(t => t.ReservationGuid == reservationGuid && t.TermsSourceId == termsSourceId);

            if (entity is null)
            {
                return false;
            }

            entity.IsAccepted = isAccepted;
            entity.AgreedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
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
    }
}
