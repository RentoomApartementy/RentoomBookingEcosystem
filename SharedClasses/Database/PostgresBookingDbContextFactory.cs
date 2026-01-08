using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RentoomBooking.SharedClasses.Database
{
   /* public class PostgresBookingDbContextFactory : IDesignTimeDbContextFactory<PostgresBookingDbContext>
    {
        public PostgresBookingDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgresBookingDbContext>();

            // Twój lokalny connection string do Postgresa
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=RentoomBookingTest;Username=postgres;Password=123");

            return new PostgresBookingDbContext(optionsBuilder.Options);
        }
    }*/
}