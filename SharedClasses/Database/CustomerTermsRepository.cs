using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Models;
using System.Collections.Generic;
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

        public async Task<List<CustomerTermsAndConditionsSource>> GetActiveTermsSourcesAsync()
        {
            return await _context.CustomerTermsSources
                .OrderBy(t => t.Id)
                .ToListAsync();
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
    }
}