using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions
{
    internal class RappInstructionsDbContext: DbContext
    {
        public RappInstructionsDbContext(DbContextOptions<RappInstructionsDbContext> options) : base(options) { }
        public DbSet<ApartmentArrivalInstructionStep> ArivalInstructions { get; set; }
    }
}
