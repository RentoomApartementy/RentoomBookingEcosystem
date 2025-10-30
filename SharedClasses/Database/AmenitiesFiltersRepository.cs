using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
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

        private const string ContainerName = "SearchFilters";
        private const string PartitionKey = "/id";
        private const string AmenitiesFilterPartitionValue = "amenities-filter";

        private const string CitiesFilterPartitionValue = "cities-filter";

        public FiltersRepository(CosmosClient client, IConfiguration configuration)
        {
            _initializationTask = InitializeAsync(client, configuration);
        }

        private async Task InitializeAsync(CosmosClient client, IConfiguration configuration)
        {
            var databaseName = configuration["AZURE_COSMOS_DATABASE_NAME"];
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new InvalidOperationException("AZURE_COSMOS_DATABASE_NAME configuration is missing.");
            }

            var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            // Ta klasa, tak jak BookingDatabase, musi mieć dostęp do kontenera
            _filterContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerName, PartitionKey));

          
        }

        public async Task SeedAmenitiesFilters()
        {
            List<SearchFilter> list = [
              new() { id = "205", name = "Garaż" },
                new () { id = "204", name = "Parking" },
                new () { id = "132", name = "Balkon" },
                new () { id = "206", name = "Zwierzęta dozwolone" },
                new () { id = "152", name = "Winda" },
                new () { id = "96", name = "Dostęp dla wózków inwalidzkich" },
                new () { id = "86", name = "Pralka" },

            ];

            var amFilters = new Dictionary<string, List<SearchFilter>>
            {
                { "pl", list }
            };

            await SaveFilters(amFilters, AmenitiesFilterPartitionValue);
        }

        public async Task SaveFilters(Dictionary<string, List<SearchFilter>> filtersDictionary,string destination_partition, ILogger? log = null)
        {

            if (filtersDictionary == null)
            {
                throw new ArgumentNullException(nameof(filtersDictionary));
            }

            await _initializationTask;

            if (_filterContainer == null)
            {
                throw new InvalidOperationException("Amenities filter container not initialized.");
            }

            var document = new SearchFilterDocument
            {
               id = destination_partition,
                filtersDictionary = filtersDictionary
            };

            try
            {
                await _filterContainer.UpsertItemAsync(document, new PartitionKey(document.id));
            }
            catch (CosmosException ex)
            {
                log?.LogError(ex, "Failed to save amenities filter values to Cosmos DB.");
                throw;
            }

        }


        public async Task<List<SearchFilterDocument>> GetAllSearchFiltersAsync(ILogger? log = null)
        {
            await _initializationTask;

            if (_filterContainer == null)
            {
                throw new InvalidOperationException("Amenities filter container not initialized.");
            }

            var results = new List<SearchFilterDocument>();

            try
            {
                var query = _filterContainer.GetItemQueryIterator<SearchFilterDocument>(
                    new QueryDefinition("SELECT * FROM c"));

                while (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync();
                    results.AddRange(response);
                }
            }
            catch (CosmosException ex)
            {
                log?.LogError(ex, "Failed to retrieve search filter documents from Cosmos DB.");
                throw;
            }

            return results;
        }

        public async Task SaveRegionsFilters(List<string?> regionNames)
        {
            List<SearchFilter> regions = [];
            
            regions.AddRange(regionNames.Select(r =>  new SearchFilter { id = r, name = r }).ToList());
            
            var amFilters = new Dictionary<string, List<SearchFilter>>
            {
                { "pl", regions }
            };

            await SaveFilters(amFilters, CitiesFilterPartitionValue);
        }




        /*   public async Task SaveAmenitiesFilterAsync(int[] filterValues, ILogger? log = null)
           {
               if (filterValues == null)
               {
                   throw new ArgumentNullException(nameof(filterValues));
               }

               await _initializationTask;

               if (_amenitiesFilterContainer == null)
               {
                   throw new InvalidOperationException("Amenities filter container not initialized.");
               }

               var document = new AmenitiesFilterDocument
               {
                   id = DocumentId,
                   amenities = filterValues
               };

               try
               {
                   await _amenitiesFilterContainer.UpsertItemAsync(document, new PartitionKey(document.id));
               }
               catch (CosmosException ex)
               {
                   log?.LogError(ex, "Failed to save amenities filter values to Cosmos DB.");
                   throw;
               }
           }
        */




    }

}