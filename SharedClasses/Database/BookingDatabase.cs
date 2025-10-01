using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Database
{
    public class BookingDatabase
    {
        private  Container _apartmentInfoContainer;
        private  Container _hashesContainer;

        private const string HashDocumentId = "all-object-hashes"; // ID for the hash document
        private const string HashPartitionKey = "/id"; // Partition
        private const string ApartmentPartitionKey = "/id"; // PartitionPaged
        public BookingDatabase(CosmosClient client, IConfiguration configuration)
        {
            InitializeAsync(client, configuration).Wait();
          
        }

             private async Task InitializeAsync(CosmosClient client, IConfiguration configuration)
        {
            
            var databaseName = configuration["AZURE_COSMOS_DATABASE_NAME"];
            var containerName = "ApartmentInfo";
            var containerNameForHashes = "ApartmentsHashes";

            var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            _apartmentInfoContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerName, "/id")  );

            _hashesContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerNameForHashes, "/id"));
        }

        public async Task<bool> HasRecordsAsync()
        {
            try
            {
                // Create a query definition to select the first item.
                // This is more efficient than selecting all items.
                var queryDefinition = new QueryDefinition("SELECT TOP 1 * FROM c");

                using (var feedIterator = _apartmentInfoContainer.GetItemQueryIterator<object>(queryDefinition))
                {
                    // Check if the query has any results.
                    // If there are more results, it means at least one record exists.
                    if (feedIterator.HasMoreResults)
                    {
                        var response = await feedIterator.ReadNextAsync();
                        return response.Any();
                    }
                }
                return false;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // This exception occurs if the container itself doesn't exist,
                // which implies there are no records.
                return false;
            }
            catch
            {
                // Handle other potential exceptions here.
                return false;
            }
        }

        public async Task BulkCreateItemsAsync(List<ApartmentObject> items, ILogger log)
        {

            int itemsPerBatch = 50;
            int totalItemsCreated = 0;
            int totalErrors = 0;
            log.LogInformation($"Starting bulk create for a total of {items.Count} items.");

            for (int i = 0; i < items.Count; i += itemsPerBatch)
            {
                var batchItems = items.Skip(i).Take(itemsPerBatch).ToList();
                var tasks = new List<Task<int>>();

                log.LogInformation($"Processing batch from index {i} to {i + batchItems.Count - 1}...");
                foreach (var item in batchItems)
                {
                    tasks.Add(ProcessCreateItemAsync(item,log));
                }

                int[] errors = await Task.WhenAll(tasks);
                int totalErrorsInBatch = errors.Sum();

                totalItemsCreated += batchItems.Count - totalErrors;
                log.LogInformation($"Successfully created {batchItems.Count - totalErrors} items in this batch. Total items created: {totalItemsCreated}.");

                //reset total errors after a batch finished
                totalErrors = 0;

                // Only delay if there are more batches to process
                if (i + itemsPerBatch < items.Count)
                {
                    log.LogInformation("Pausing for 1 second to manage throughput...");
                    await Task.Delay(1000);
                }
            }

            log.LogInformation($"Completed bulk create. A total of {totalItemsCreated} items were successfully created.");
        }

        public async Task BulkReplaceItemsAsync(List<ApartmentObject> items,  ILogger _logger)
        {
            int itemsPerBatch = 50;
            int totalItemsReplaced = 0;

            _logger.LogInformation($"Starting bulk replace for a total of {items.Count} items.");

            for (int i = 0; i < items.Count; i += itemsPerBatch)
            {
                var batchItems = items.Skip(i).Take(itemsPerBatch).ToList();
              
                var tasks = new List<Task<int>>();

                _logger.LogInformation($"Processing batch from index {i} to {i + batchItems.Count - 1}...");

                foreach (var item in batchItems)
                {
                 
                    tasks.Add(ProcessReplaceItemAsync(item, _logger));
                  //  tasks.Add(ProcessReplaceItemWithChangeDetectionAsync(item, _logger));
                    
                }

               
                int[] errors = await Task.WhenAll(tasks);
                int totalErrorsInBatch = errors.Sum();

                totalItemsReplaced += batchItems.Count - totalErrorsInBatch;
                _logger.LogInformation($"Successfully replaced {batchItems.Count - totalErrorsInBatch} items in this batch. Total items replaced: {totalItemsReplaced}.");

            
                if (i + itemsPerBatch < items.Count)
                {
                    _logger.LogInformation("Pausing for 1 second to manage throughput...");
                    await Task.Delay(1000);
                }
            }

            _logger.LogInformation($"Completed bulk replace. A total of {totalItemsReplaced} items were successfully replaced.");
        }

        
        private async Task<int> ProcessReplaceItemAsync(ApartmentObject item, ILogger logger)
        {
            try
            {
                await _apartmentInfoContainer.ReplaceItemAsync(item, item.Id, new PartitionKey(item.Id));
                return 0; // Success
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.LogWarning($"Item with ID '{item.Id}' not found. Cannot replace. A 'create' or 'upsert' operation might be needed.");
                }
                else if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    logger.LogError($"An unexpected conflict occurred for item with ID '{item.Id}'. Resource already exists.");
                }
                else
                {
                    logger.LogError(ex, $"Failed to replace item with ID '{item.Id}'. Status Code: {ex.StatusCode}");
                }
                return 1; // Error
            }
        }


        public async Task<List<ItemHash>> GetExistingHashesAsync(ILogger log)
        {
            try
            {
                var response = await _hashesContainer.ReadItemAsync<ApartmentObjectHash>(HashDocumentId, new PartitionKey(HashDocumentId));
                return response.Resource.Hashes;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                log.LogInformation("Hash document not found. Creating a new one.");
                return new List<ItemHash>();
            }
            catch (Exception ex)
            {
                log.LogError($"Error reading hash document: {ex.Message}");
                return new List<ItemHash>();
            }
        }

        public async Task UpdateHashesDocumentAsync(List<ItemHash> hashes, ILogger log)
        {
            var objectHashes = new ApartmentObjectHash
            {
                Hashes = hashes,
                lastUpdated = DateTime.UtcNow
            };

            try
            {
                await _hashesContainer.UpsertItemAsync(objectHashes, new PartitionKey(HashDocumentId));
                log.LogInformation("Object hashes document updated successfully.");
            }
            catch (Exception ex)
            {
                log.LogError($"Error updating hash document: {ex.Message}");
            }
        }

       
        private async Task<int> ProcessCreateItemAsync(ApartmentObject item, ILogger _logger)
        {
            try
            {
                await _apartmentInfoContainer.CreateItemAsync(item, new PartitionKey(item.Id));
                return 0; // Success
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning($"Item with ID '{item.Id}' not found. Cannot replace. A 'create' or 'upsert' operation might be needed.");
                }
                else if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    _logger.LogError($"An unexpected conflict occurred for item with ID '{item.Id}'. Resource already exists.");
                }
                else
                {
                    _logger.LogError(ex, $"Failed to replace item with ID '{item.Id}'. Status Code: {ex.StatusCode}");
                }
                return 1; // Error
            }
        }


        private async Task<int> ProcessReplaceItemWithChangeDetectionAsync(ApartmentObject newItem, ILogger _logger)
        {
            try
            {
                // First, read the existing item to compare it.
                var oldItemResponse = await _apartmentInfoContainer.ReadItemAsync<ApartmentObject>(newItem.Id, new PartitionKey(newItem.Id));
                var oldItem = oldItemResponse.Resource;

                // Compare the new and old items for changes.
                string differences = FindDifferences(oldItem, newItem);

                if (string.IsNullOrEmpty(differences))
                {
                    _logger.LogInformation($"Item with ID '{newItem.Id}' has no changes. Skipping replace operation.");
                  
                }
                else
                {
                    _logger.LogInformation($"Item with ID '{newItem.Id}' has changed. Differences: {differences}");
                    await _apartmentInfoContainer.ReplaceItemAsync(newItem, newItem.Id, new PartitionKey(newItem.Id));
                    
                }
                return 0;
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // If the item doesn't exist, we can't replace it.
                    _logger.LogWarning($"Item with ID '{newItem.Id}' not found. Cannot replace. A 'create' operation might be needed.");
                }
                else
                {
                    _logger.LogError(ex, $"Failed to replace item with ID '{newItem.Id}'. Status Code: {ex.StatusCode}");
                }
                return 1;
            }
        }

       
        private string FindDifferences(ApartmentObject oldObj, ApartmentObject newObj)
        {
            if (oldObj == null || newObj == null)
            {
                return "One or both objects are null, cannot compare.";
            }
            var oldObjAsSstring =JsonConvert.SerializeObject(oldObj);
            var newObjAsString = JsonConvert.SerializeObject(newObj);
            var differences = new StringBuilder();
            var properties = typeof(ApartmentObject).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var oldValue = property.GetValue(oldObj)?.ToString();
                var newValue = property.GetValue(newObj)?.ToString();

                if (oldValue != newValue)
                {
                    differences.AppendLine($"  - Property '{property.Name}': Old='{oldValue ?? "null"}', New='{newValue ?? "null"}'");
                }
            }

            return differences.Length > 0 ? differences.ToString() : null;
        }


        public async Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(
        string? continuationToken,
        int pageSize)
        {
            var sql = "SELECT * FROM c";
            var qd = new QueryDefinition(sql);

            var opts = new QueryRequestOptions
            {
                MaxItemCount = pageSize,
               // PartitionKey = new PartitionKey(ApartmentPartitionKey)
            };

            var it = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(qd, continuationToken, opts);
            if (!it.HasMoreResults)
                return new PagedResult<ApartmentObject>(Array.Empty<ApartmentObject>(), null);

            var page = await it.ReadNextAsync();
            return new PagedResult<ApartmentObject>(page.ToList(), page.ContinuationToken);
        }

      

    }
}
