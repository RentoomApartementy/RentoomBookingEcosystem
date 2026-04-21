using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database
{
    public class RappEventsDbContextFactory : IDbContextFactory<RappEventsDbContext>
    {
        private readonly IDbContextFactory<RappEventsDbContext> _innerFactory;

        public RappEventsDbContextFactory(IDbContextFactory<RappEventsDbContext> innerFactory)
        {
            _innerFactory = innerFactory;
        }

        public RappEventsDbContext CreateDbContext()
        {
            return _innerFactory.CreateDbContext();
        }

        public Task<RappEventsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return _innerFactory.CreateDbContextAsync(cancellationToken);
        }
    }


    /*    public class RappEventsDbContextFactory: IDesignTimeDbContextFactory<RappEventsDbContext>
        {
          public RappEventsDbContext CreateDbContext(string[] args)
            {
                var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__RentoomDbConnectionString")
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings:RentoomDbConnectionString")
                    ?? "Host=localhost;Port=5432;Database=rentoomdb;Username=postgres;Password=admin";

                var options = new DbContextOptionsBuilder<RappEventsDbContext>()
                    .UseNpgsql(connectionString)
                    .Options;

                return new RappEventsDbContext(options);
            }
        }
    */
}
