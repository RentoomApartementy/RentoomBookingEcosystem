using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System.Threading;

namespace RentoomBooking.SharedClasses.Database
{
    public class ApartmentRepository
    {
        private Container? _apartmentInfoContainer;
        private readonly Task _initializationTask;

        private const string ContainerName = "ApartmentInfo";
        private const string PartitionKey = "/partitionKey";
        private const string PartitionKeyValue = "rentoom-apartments-list";
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
            var db = database.Database;
            // Ta klasa, tak jak BookingDatabase, musi mieć dostęp do kontenera
            _apartmentInfoContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerName, PartitionKey));


        }

        public async Task<long> GetApartmentCountAsync(ILogger? log = null)
        {
            await _initializationTask;
            long totalCount = 0;
            try
            {
                var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c");

                var queryOptions = new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(PartitionKeyValue)
                };



                using var countIt = _apartmentInfoContainer.GetItemQueryIterator<long>(query, requestOptions: queryOptions);
                if (countIt.HasMoreResults)
                {
                    var countPage = await countIt.ReadNextAsync();
                    totalCount = countPage.FirstOrDefault();
                }

                return totalCount;
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
                apartmentList.ForEach(a => a.PartitionKey = PartitionKeyValue);
                

            log.LogInformation("Purging ApartmentInfo container before inserting new data.");

            try
            {
                long count = await GetApartmentCountAsync(log);
                log.LogInformation("{count} records to be purged", count);


                //await PurgeContainerAsync(log, cancellationToken);
                await PurgeAll();
                count = await GetApartmentCountAsync(log);
                log.LogInformation("{count} after purge in container", count);
                
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
            _logger.LogInformation("Purging logical partition '{pk}' in ApartmentRepository...", PartitionKeyValue);

            ResponseMessage deleteResponse = await _apartmentInfoContainer!.DeleteAllItemsByPartitionKeyStreamAsync(new PartitionKey(PartitionKeyValue));

            if (deleteResponse.IsSuccessStatusCode)
                _logger.LogInformation("Partition '{pk}' delete started successfully.", PartitionKeyValue);
            else
                _logger.LogWarning("Partition delete returned {code}: {msg}", deleteResponse.StatusCode, deleteResponse.ErrorMessage);
        }

      /*  private async Task PurgeContainerAsync(ILogger log, CancellationToken cancellationToken)
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
      */

        public async Task BulkCreateItemsAsync(List<ApartmentObject> items, ILogger log)
        {
            await _initializationTask;
               if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");
            await PurgeAll();

            int itemsPerBatch = 25;
            int totalItemsCreated = 0;
            log.LogInformation($"Starting bulk create for a total of {items.Count} items.");
            
            foreach (var a in items) a.PartitionKey = PartitionKeyValue;

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
        
        
        private readonly int MaxCreateRetries = 6;
        private async Task<int> ProcessCreateItemAsync(ApartmentObject item, ILogger _logger, CancellationToken ct = default)
        {
            for (int attempt = 0; attempt <= MaxCreateRetries; attempt++)
            {
                try
                {
                    var resp = await _apartmentInfoContainer!
                        .CreateItemAsync(item, new PartitionKey(PartitionKeyValue), cancellationToken: ct);

                    _logger.LogDebug("Created {Id}. RU {RU:F2}", item.Id, resp.RequestCharge);
                    return 0;
                }
                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    var delay = Jitter(ex.RetryAfter ?? TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
                    _logger.LogWarning("429 on {Id} (attempt {Attempt}). Backing off {Delay}. ActivityId {ActivityId}",
                                      item.Id, attempt + 1, delay, ex.ActivityId);
                    if (attempt == MaxCreateRetries) { _logger.LogError(ex, "Giving up on {Id}", item.Id); return 1; }
                    await Task.Delay(delay, ct);
                }
            }
            return 1;


            /*  try
            {
                item.PartitionKey = PartitionKeyValue;

                await _apartmentInfoContainer!.CreateItemAsync(item, new PartitionKey(PartitionKeyValue));
                return 0;
            }
            catch (CosmosException ex)
            {
                
                _logger.LogError(ex,
                    "Create failed for id {Id}. Status:{Status} Sub:{Sub} ActivityId:{ActivityId} Msg:{Msg}",
                    item.Id, ex.StatusCode, ex.SubStatusCode, ex.ActivityId, ex.Message);
                return 1;
            }*/
        }

        private static TimeSpan Jitter(TimeSpan baseDelay)
        {
            // +/- 20% jitter
            var ms = baseDelay.TotalMilliseconds;
            var jitter = (0.8 + Random.Shared.NextDouble() * 0.4) * ms;
            return TimeSpan.FromMilliseconds(jitter);
        }

        public async Task<ApartmentObject?> FindApartmentInCosmosDb(int apartmentId, CancellationToken cancellationToken = default)
        {
            await _initializationTask;
            var objectId = apartmentId.ToString();

            if (_apartmentInfoContainer is null)
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");


            try
            {
            
                ItemResponse<ApartmentObject> response = await _apartmentInfoContainer.ReadItemAsync<ApartmentObject>(
                    id: objectId, 
                    partitionKey: new PartitionKey(PartitionKeyValue),
                    cancellationToken: cancellationToken);

                var apartment = response.Resource;
                
                return apartment;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Apartment with id {ApartmentId} not found in partition {PK}.", apartmentId, PartitionKeyValue);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving apartment JSON for id {ApartmentId}.", apartmentId);
                throw;
            }
        }

        public async Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(string? continuationToken, int pageSize)
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");

            var sql = "SELECT * FROM c";
            var qd = new QueryDefinition(sql);

            var opts = new QueryRequestOptions
            {
                MaxItemCount = pageSize,
                PartitionKey = new PartitionKey(PartitionKeyValue)
            };

            long totalCount = await GetApartmentCountAsync();

            var it = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(qd, continuationToken, opts);
            if (!it.HasMoreResults)
                return new PagedResult<ApartmentObject>(Array.Empty<ApartmentObject>(), null, 0, totalCount);

            var page = await it.ReadNextAsync();
            return new PagedResult<ApartmentObject>(page.ToList(), page.ContinuationToken, page.Count, totalCount);
        }
    }

}