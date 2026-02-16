using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public  class TermsRepository
    {
        private ILogger<TermsRepository> _logger;
        private PostgresBookingDatabase _postgresBookingDatabase;
        private IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;

        public TermsRepository(PostgresBookingDatabase postgresBookingDatabase, IDbContextFactory<PostgresBookingDbContext> dbContextFactory, IConfiguration configuration, ILogger<TermsRepository> logger)
        {
            _postgresBookingDatabase = postgresBookingDatabase;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<TermsEntity?> GetTermsByResTokenAsync(string resToken, CancellationToken cancellationToken = default)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            return await context.Terms.AsNoTracking().FirstOrDefaultAsync(t => t.ResToken == resToken, cancellationToken);
        }

        public async Task AddTermsAsync(TermsEntity entity, CancellationToken cancellationToken = default)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            context.Terms.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateTermsAsync(TermsEntity entity, CancellationToken cancellationToken = default)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var existing = await context.Terms.FirstOrDefaultAsync(t => t.ResToken == entity.ResToken, cancellationToken);
            if (existing == null)
            {
                throw new InvalidOperationException($"Cannot update, there is no record with ID: '{entity.ResToken}'");
            }
            context.Entry(existing).CurrentValues.SetValues(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

    }
}
