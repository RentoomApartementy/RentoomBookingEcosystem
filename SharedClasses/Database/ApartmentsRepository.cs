using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class ApartmentRepository
    {
        private Container? _apartmentInfoContainer;
        private readonly Task _initializationTask;

        private const string ContainerName = "ApartmentInfo";
        private const string PartitionKey = "/id";

        public ApartmentRepository(CosmosClient client, IConfiguration configuration)
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
            _apartmentInfoContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerName, PartitionKey));
        }
        
        public async Task<long> GetApartmentCountAsync(ILogger? log = null)
        {
            await _initializationTask;

            try
            {
                var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");
                var queryIterator = _apartmentInfoContainer.GetItemQueryIterator<long>(query);

                long count = 0;
                if (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ReadNextAsync();
                    count = response.FirstOrDefault();
                }
                
                return count;
            }
            catch (CosmosException ex)
            {
                log?.LogError(ex, "Failed to get item count from ApartmentInfo container.");
                return 0;
            }
        }
    }
}