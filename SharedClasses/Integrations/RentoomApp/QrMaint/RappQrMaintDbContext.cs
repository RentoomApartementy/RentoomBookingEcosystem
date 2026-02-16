using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint
{
    public class QrMaintRappDbContext : DbContext
    {
        public QrMaintRappDbContext(DbContextOptions<QrMaintRappDbContext> options) : base(options) { }

        public DbSet<QRMaintIdosellMappingEntity> QRMaintIdosellMapping { get; set; }
        public DbSet<RentoomQREntity> RentoomQRs { get; set; }

        public DbSet<ApartmentItemLocalSettings> ApartmentItemLocalSettings { get; set; }
    }
}
