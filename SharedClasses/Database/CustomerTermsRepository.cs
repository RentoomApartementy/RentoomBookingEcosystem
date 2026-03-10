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

        public async Task<List<CustomerTermDisplayDto>> GetActiveTermsSourcesAsync(string? cultureName)
        {
            var normalizedCulture = NormalizeCulture(cultureName);
            var neutralCulture = normalizedCulture.Split('-')[0];

            var sources = await _context.CustomerTermsSources
                .Where(t => t.IsActive)
                .Include(t => t.Translations)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Id)
                .ToListAsync();

            return sources.Select(source =>
            {
                var translation = source.Translations
                    .FirstOrDefault(t => string.Equals(t.Culture, normalizedCulture, System.StringComparison.OrdinalIgnoreCase))
                    ?? source.Translations.FirstOrDefault(t => string.Equals(t.Culture, neutralCulture, System.StringComparison.OrdinalIgnoreCase))
                    ?? source.Translations.FirstOrDefault(t => string.Equals(t.Culture, "pl", System.StringComparison.OrdinalIgnoreCase))
                    ?? source.Translations.FirstOrDefault();

                return new CustomerTermDisplayDto
                {
                    Id = source.Id,
                    IsRequired = source.IsRequired,
                    Description = translation?.Description ?? source.Description,
                    Link = translation?.Link ?? source.Link
                };
            }).ToList();
        }

        public async Task AddAgreedTermsAsync(IEnumerable<CustomerAgreedTerms> agreedTerms)
        {
            await _context.CustomerAgreedTerms.AddRangeAsync(agreedTerms);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(Guid reservationGuid)
        {
            return await _context.CustomerAgreedTerms
                .Where(t => t.ReservationGuid == reservationGuid)
                .Include(t => t.TermsSource)
                .Select(t => new CustomerAgreedTermDto
                {
                    TermsSourceId = t.TermsSourceId,
                    Description = t.TermsSource.Description,
                    AgreedAt = t.AgreedAt,
                    IsRequired = t.TermsSource.IsRequired,
                    IsAccepted = t.IsAccepted
                })
                .ToListAsync();
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
    }
}
