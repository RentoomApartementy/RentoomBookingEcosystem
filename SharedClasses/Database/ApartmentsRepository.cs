using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Database
{
    public class ApartmentRepository
    {
        private Container? _apartmentInfoContainer;
        private readonly Task _initializationTask;

        private const string ContainerName = "ApartmentInfo";
        private const string PartitionKey = "/id";
        private ILogger<ApartmentRepository> _logger;

        public ApartmentRepository(CosmosClient client, IConfiguration configuration, ILogger<ApartmentRepository> logger)
        {
            _initializationTask = InitializeAsync(client, configuration);
            _logger = logger;
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
        public async Task SaveApartmentsAsync(IEnumerable<ApartmentObject> apartments, ILogger log, CancellationToken cancellationToken = default)
        {
            if (apartments == null) throw new ArgumentNullException(nameof(apartments));
            if (log == null) throw new ArgumentNullException(nameof(log));

            await _initializationTask;

            if (_apartmentInfoContainer == null)
            {
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");
            }

            var apartmentList = apartments.ToList();

            log.LogInformation("Purging ApartmentInfo container before inserting new data.");

            try
            {
                await PurgeContainerAsync(log, cancellationToken);
                log.LogInformation("ApartmentInfo container purged successfully. Starting bulk insert of {count} apartments.", apartmentList.Count);
                await BulkCreateItemsAsync(apartmentList, log);
                log.LogInformation("Bulk insert of apartments completed successfully.");
            }
            catch (CosmosException ex)
            {
                log.LogError(ex, "Failed to refresh ApartmentInfo container with new apartments.");
                throw;
            }
        }
        private async Task PurgeAll()
        {
            _logger.LogInformation("Purge All in ApartmentRepository has started");
            ResponseMessage deleteResponse = await _apartmentInfoContainer!.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey(PartitionKey));

            if (deleteResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Delete all documents with partition key operation has successfully started");
            }
            _logger.LogInformation("Purge All in ApartmentRepository has ended");
        }

        private async Task PurgeContainerAsync(ILogger log, CancellationToken cancellationToken)
        {
            var query = new QueryDefinition("SELECT c.id FROM c");
            var iterator = _apartmentInfoContainer!.GetItemQueryIterator<dynamic>(query, requestOptions: new QueryRequestOptions
            {
                MaxItemCount = 100
            });

            var deleteTasks = new List<Task>();

            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);

                foreach (var item in page)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string id = item.id;
                    deleteTasks.Add(_apartmentInfoContainer.DeleteItemAsync<dynamic>(id, new PartitionKey(id), cancellationToken: cancellationToken));

                    if (deleteTasks.Count >= 50)
                    {
                        await Task.WhenAll(deleteTasks);
                        deleteTasks.Clear();
                    }
                }
            }

            if (deleteTasks.Count > 0)
            {
                await Task.WhenAll(deleteTasks);
                deleteTasks.Clear();
            }

            log.LogInformation("ApartmentInfo container purge complete.");
        }


        public async Task BulkCreateItemsAsync(List<ApartmentObject> items, ILogger log)
        {
            await _initializationTask;
               if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");
            await PurgeAll();

            int itemsPerBatch = 25;
            int totalItemsCreated = 0;
            log.LogInformation($"Starting bulk create for a total of {items.Count} items.");

            for (int i = 0; i < items.Count; i += itemsPerBatch)
            {
                var batchItems = items.Skip(i).Take(itemsPerBatch).ToList();
                var tasks = batchItems.Select(item => ProcessCreateItemAsync(item, log)).ToList();

                int[] errors = await Task.WhenAll(tasks);
                int totalErrorsInBatch = errors.Sum();

                totalItemsCreated += batchItems.Count - totalErrorsInBatch;
                log.LogInformation($"Successfully created {batchItems.Count - totalErrorsInBatch} items in this batch. Total items created: {totalItemsCreated}.");

                if (i + itemsPerBatch < items.Count)
                {
                    log.LogInformation("Pausing for 2 second to manage throughput...");
                    await Task.Delay(2000);
                }
            }

            log.LogInformation($"Completed bulk create. A total of {totalItemsCreated} items were successfully created.");
        }

        private async Task<int> ProcessCreateItemAsync(ApartmentObject item, ILogger _logger)
        {
            try
            {
                await _apartmentInfoContainer!.CreateItemAsync(item, new PartitionKey(item.Id));
                return 0;
            }
            catch (CosmosException ex)
            {
                // ... obsługa błędów bez zmian
                return 1;
            }
        }

    }

}