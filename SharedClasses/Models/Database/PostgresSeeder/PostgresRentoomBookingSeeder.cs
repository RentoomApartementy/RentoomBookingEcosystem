using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Models.Database.PostgresSeeder
{
    public static class PostgresRentoomBookingSeeder
    {

        public static async Task SeedAsync(PostgresBookingDbContext context)
        {

            await context.Database.MigrateAsync();
            await SeedAmenitiesFilters(context);

        }

        private const string AmenitiesFilterGroupName = "amenities-filter";

        private const string CitiesFilterPartitionValue = "city-regions-filter";
        private static async Task SeedAmenitiesFilters(PostgresBookingDbContext context)
        {
            List<SearchFilter> list = [
              new() { id = "205", name = "Garaż",icon_materialui_name = "garage_home" },
                new () { id = "204", name = "Parking", icon_materialui_name="parking_sign"},
                new () { id = "132", name = "Balkon", icon_materialui_name ="balcony" },
                new () { id = "206", name = "Zwierzęta dozwolone" , icon_materialui_name="pets"},
                new () { id = "152", name = "Winda", icon_materialui_name="elevator" },
                new () { id = "96", name = "Dostęp dla wózków inwalidzkich",icon_materialui_name="accessible" },
                new () { id = "86", name = "Pralka" ,icon_materialui_name="local_laundry_service"},

            ];

          /*  var amFilters = new Dictionary<string, List<SearchFilter>>
            {
                { "pl", list }
            };
            var entity = new SearchFiltersEntity()
            {
                FilterGroupName = "AmenitiesFilterGroupName",
                Payload = JsonCo amFilters
            }


           await context*/
        }
          

    }

    public static class SearchFiltersSeedData
    {
        public const string AmenitiesFilterGroupName = "amenities-filter";
        public const string CitiesFilterGroupName = "city-regions-filter";

        public static Dictionary<string, List<SearchFilter>> BuildAmenitiesFilters()
        {
            List<SearchFilter> list =
            [
                new() { id = "205", name = "Garaż", icon_materialui_name = "garage_home" },
                new() { id = "204", name = "Parking", icon_materialui_name = "parking_sign" },
                new() { id = "132", name = "Balkon", icon_materialui_name = "balcony" },
                new() { id = "206", name = "Zwierzęta dozwolone", icon_materialui_name = "pets" },
                new() { id = "152", name = "Winda", icon_materialui_name = "elevator" },
                new() { id = "96", name = "Dostęp dla wózków inwalidzkich", icon_materialui_name = "accessible" },
                new() { id = "86", name = "Pralka", icon_materialui_name = "local_laundry_service" },
            ];

            return new Dictionary<string, List<SearchFilter>>
            {
                { "pl", list }
            };
        }

        


    }
}
