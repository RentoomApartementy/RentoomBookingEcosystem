using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class AmenitiesRepository
    {
        private Container? _amenitiesFilterContainer;
        private readonly Task _initializationTask;

        private const string ContainerName = "AmenitiesFilter";
        private const string PartitionKey = "/id";
        private const string DocumentId = "amenities-filter";


        public AmenitiesRepository(CosmosClient client, IConfiguration configuration)
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
            _amenitiesFilterContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerName, PartitionKey));

            _amenitiesFilterContainer = await database.Database.CreateContainerIfNotExistsAsync(
               new ContainerProperties(ContainerName, PartitionKey));

           // int[] list = [205, 204, 132, 205, 204, 132, 206, 152, 96, 86];
          //  await SaveAmenitiesFilterAsync(list);

        }

        public async Task SaveAmenitiesFilterAsync(int[] filterValues, ILogger? log = null)
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


        public async Task<int[]> GetAmenitiesFilterAsync(ILogger? log = null)
        {
            await _initializationTask;

            if (_amenitiesFilterContainer == null)
            {
                throw new InvalidOperationException("Amenities filter container not initialized.");
            }

            try
            {
                var response = await _amenitiesFilterContainer.ReadItemAsync<AmenitiesFilterDocument>(
                    DocumentId,
                    new PartitionKey(DocumentId));

                return response.Resource.amenities ?? Array.Empty<int>();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                log?.LogInformation("Amenities filter document not found in Cosmos DB.");
                return Array.Empty<int>();
            }
            catch (CosmosException ex)
            {
                log?.LogError(ex, "Failed to retrieve amenities filter values from Cosmos DB.");
                throw;
            }
        }



    }

}