using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.PostgresSeeder;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class FiltersRepository
    {
        private Container? _filterContainer;
        // private Container? _citiesFilterContainer;
        private readonly Task _initializationTask;

        //private const string ContainerName = "SearchFilters";
        //private const string PartitionKey = "/id";
        private const string AmenitiesFilterPartitionValue = SearchFiltersSeedData.AmenitiesFilterGroupName;

        PostgresBookingDatabase _postgresBookingDatabase;

        private const string CitiesFilterPartitionValue = SearchFiltersSeedData.CitiesFilterGroupName;


        public FiltersRepository(PostgresBookingDatabase postgresBookingDatabase, IConfiguration configuration)
        {
            _postgresBookingDatabase = postgresBookingDatabase;
        }


        public async Task SeedAmenitiesFilters(ILogger log)
        {
            var amFilters = SearchFiltersSeedData.BuildAmenitiesFilters();
            await _postgresBookingDatabase.SaveSearchFiltersAsync(amFilters, AmenitiesFilterPartitionValue, log);

        }

        public async Task SaveRegionsFilters(List<string?> regionNames, ILogger? log = null)
        {
            List<SearchFilter> regions = [];

            regions.AddRange(regionNames.Select(r =>
            {

                var iconName = "map";
                if (r.ToLower() == "centrum") iconName = "location_city";
                if (r.ToLower() == "stare miasto") iconName = "castle";

                return new SearchFilter { id = r, name = r, icon_materialui_name = iconName };

            }
            ).ToList());

            var amFilters = new Dictionary<string, List<SearchFilter>>
            {
                { "pl", regions }
            };

            await _postgresBookingDatabase.SaveSearchFiltersAsync(amFilters, CitiesFilterPartitionValue, log);
        }

        public async Task<List<SearchFilterDocument>> GetAllSearchFiltersAsync(ILogger? log = null)
        {
            return await _postgresBookingDatabase.GetAllSearchFiltersAsync(log);
        }

    }

}