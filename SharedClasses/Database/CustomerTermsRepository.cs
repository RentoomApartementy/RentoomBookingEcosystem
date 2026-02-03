using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Models;
using System.Collections.Generic;
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
    }
}