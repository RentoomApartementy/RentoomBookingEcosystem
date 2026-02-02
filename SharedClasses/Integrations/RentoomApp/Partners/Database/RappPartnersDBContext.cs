using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database
{
    public class RappPartnersDBContext :DbContext
    {
        public RappPartnersDBContext(DbContextOptions<RappPartnersDBContext> options) : base(options) { }

        public DbSet<Partner> Partners { get; set; }
        public DbSet<PartnerSupportedLanguage> PartnerSupportedLanguages { get; set; }
        public DbSet<PartnerService> PartnerServices { get; set; }
        public DbSet<PartnerServiceTranslation> PartnerServiceTranslations { get; set; }
        public DbSet<MediaAsset> MediaAssets { get; set; }
        public DbSet<PartnerServiceBanner> PartnerServiceBanners { get; set; }
        public DbSet<PartnerServiceTarget> PartnerServiceTargets { get; set; }


    }
}
