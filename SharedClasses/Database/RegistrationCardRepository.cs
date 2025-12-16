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
    public class RegistrationCardRepository
    {
        private ILogger<RegistrationCardRepository> _logger;
        private PostgresBookingDatabase _postgresBookingDatabase;
        private IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;

        public RegistrationCardRepository(PostgresBookingDatabase postgresBookingDatabase, IDbContextFactory<PostgresBookingDbContext> dbContextFactory, IConfiguration configuration, ILogger<RegistrationCardRepository> logger)
        {
            _postgresBookingDatabase = postgresBookingDatabase;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<RegistrationCardEntity?> GetRegistrationCardByResTokenAsync(string resToken)
        {
            try
            {
                await using var context = _dbContextFactory.CreateDbContext();
                var entity = await context.RegistrationCard
                    .AsNoTracking()
                    .FirstOrDefaultAsync(tc => tc.ResToken == resToken);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "can't get registration card for restoken: {resToken}", resToken);
                throw;
            }

        }

        public async Task SaveRegistrationCardAsync(RegistrationCardEntity entity)
        {
            try
            {
                await using var context = _dbContextFactory.CreateDbContext();
                var existingCard = await context.RegistrationCard
                    .FirstOrDefaultAsync(c => c.ResToken == entity.ResToken);

                if (existingCard != null)
                {
                    existingCard.ContactEmail = entity.ContactEmail;
                    //existingCard.ContactPhone = entity.ContactPhone;
                    //existingCard.PhoneCountryCode = entity.PhoneCountryCode;
                    existingCard.CheckInTime = entity.CheckInTime;
                    existingCard.GuestsData = entity.GuestsData;
                }
                else
                {
                    context.RegistrationCard.Add(entity);
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "can't save registrationcard for restoken: {resToken}", entity.ResToken);
                throw;
            }
        }

    }
}
