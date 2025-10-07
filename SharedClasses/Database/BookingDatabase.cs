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
        // Kontenery są teraz nullowalne, bo nie są inicjowane synchronicznie w konstruktorze
        private Container? _apartmentInfoContainer;
        private Container? _hashesContainer;
        private Container? _reservationsContainer;

        // Prywatne pole przechowujące zadanie inicjalizacji
        private readonly Task _initializationTask;

        private const string HashDocumentId = "all-object-hashes";
        private const string ReservationPartitionKey = "/resToken";

        // ZMODYFIKOWANY KONSTRUKTOR
        public BookingDatabase(CosmosClient client, IConfiguration configuration)
        {
            // Uruchamiamy inicjalizację, ale nie czekamy na nią blokująco.
            // Zapisujemy zadanie (Task) w polu, aby można było na nie poczekać później.
            _initializationTask = InitializeAsync(client, configuration);
        }

        private async Task InitializeAsync(CosmosClient client, IConfiguration configuration)
        {
            var databaseName = configuration["AZURE_COSMOS_DATABASE_NAME"];
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new InvalidOperationException("AZURE_COSMOS_DATABASE_NAME configuration is missing.");
            }
            
            var containerName = "ApartmentInfo";
            var containerNameForHashes = "ApartmentsHashes";

            var database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            _apartmentInfoContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerName, "/id"));

            _hashesContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(containerNameForHashes, "/id"));

            _reservationsContainer = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties("Reservations", ReservationPartitionKey));
        }

        // --- PUBLICZNE METODY ZMIENIONE TAK, ABY CZEKAŁY NA INICJALIZACJĘ ---

        public async Task<bool> HasRecordsAsync()
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");

            try
            {
                var queryDefinition = new QueryDefinition("SELECT TOP 1 * FROM c");

                using (var feedIterator = _apartmentInfoContainer.GetItemQueryIterator<object>(queryDefinition))
                {
                    if (feedIterator.HasMoreResults)
                    {
                        var response = await feedIterator.ReadNextAsync();
                        return response.Any();
                    }
                }
                return false;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task BulkCreateItemsAsync(List<ApartmentObject> items, ILogger log)
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");
            
            int itemsPerBatch = 50;
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
                    log.LogInformation("Pausing for 1 second to manage throughput...");
                    await Task.Delay(1000);
                }
            }

            log.LogInformation($"Completed bulk create. A total of {totalItemsCreated} items were successfully created.");
        }

        public async Task BulkReplaceItemsAsync(List<ApartmentObject> items, ILogger _logger)
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");

            int itemsPerBatch = 50;
            int totalItemsReplaced = 0;
            _logger.LogInformation($"Starting bulk replace for a total of {items.Count} items.");

            for (int i = 0; i < items.Count; i += itemsPerBatch)
            {
                var batchItems = items.Skip(i).Take(itemsPerBatch).ToList();
                var tasks = batchItems.Select(item => ProcessReplaceItemAsync(item, _logger)).ToList();

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

        public async Task<List<ItemHash>> GetExistingHashesAsync(ILogger log)
        {
            await _initializationTask;
            if (_hashesContainer == null) throw new InvalidOperationException("Hashes container not initialized.");

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
            await _initializationTask;
            if (_hashesContainer == null) throw new InvalidOperationException("Hashes container not initialized.");

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

        public async Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(string? continuationToken, int pageSize)
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");

            var sql = "SELECT * FROM c";
            var qd = new QueryDefinition(sql);

            var opts = new QueryRequestOptions { MaxItemCount = pageSize };

            var it = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(qd, continuationToken, opts);
            if (!it.HasMoreResults)
                return new PagedResult<ApartmentObject>(Array.Empty<ApartmentObject>(), null);

            var page = await it.ReadNextAsync();
            return new PagedResult<ApartmentObject>(page.ToList(), page.ContinuationToken);
        }

        public async Task<RentoomReservation> GetRentoomReservationByResTokenAsync(string resToken, ILogger log)
        {
            await _initializationTask;
            if (_reservationsContainer == null) throw new InvalidOperationException("Reservations container not initialized.");

            try
            {
                var response = await _reservationsContainer.ReadItemAsync<RentoomReservation>(
                    id: resToken,
                    partitionKey: new PartitionKey(resToken));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                log?.LogWarning("Reservation with resId {resToken} not found.", resToken);
                return null;
            }
        }

        public async Task<string?> SaveReservationJsonAsync(Reservation payloadReservation, ILogger log)
        {
            await _initializationTask;
            if (_reservationsContainer == null) throw new InvalidOperationException("Reservations container not initialized.");

            string resToken = Guid.NewGuid().ToString("N");
            var doc = new RentoomReservation
            {
                Id = resToken,
                ResToken = resToken,
                Reservation = payloadReservation
            };

            try
            {
                await _reservationsContainer.CreateItemAsync(doc, new PartitionKey(resToken));
                log.LogInformation("Saved reservation {SourceReservationId} as resId {resToken}.", payloadReservation.id, resToken);
                return resToken;
            }
            catch (CosmosException ex)
            {
                log.LogError(ex, "Failed to save reservation {0} to Cosmos.", payloadReservation.id);
                return null;
            }
        }

        // --- METODY PRYWATNE (nie muszą czekać na _initializationTask, bo metody publiczne już to zrobiły) ---

        private async Task<int> ProcessReplaceItemAsync(ApartmentObject item, ILogger logger)
        {
            try
            {
                await _apartmentInfoContainer!.ReplaceItemAsync(item, item.Id, new PartitionKey(item.Id));
                return 0;
            }
            catch (CosmosException ex)
            {
                // ... obsługa błędów bez zmian
                return 1;
            }
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

        private async Task<int> ProcessReplaceItemWithChangeDetectionAsync(ApartmentObject newItem, ILogger _logger)
        {
            try
            {
                var oldItemResponse = await _apartmentInfoContainer!.ReadItemAsync<ApartmentObject>(newItem.Id, new PartitionKey(newItem.Id));
                var oldItem = oldItemResponse.Resource;
                string? differences = FindDifferences(oldItem, newItem);

                if (string.IsNullOrEmpty(differences))
                {
                    _logger.LogInformation($"Item with ID '{newItem.Id}' has no changes. Skipping replace operation.");
                }
                else
                {
                    _logger.LogInformation($"Item with ID '{newItem.Id}' has changed. Differences: {differences}");
                    await _apartmentInfoContainer!.ReplaceItemAsync(newItem, newItem.Id, new PartitionKey(newItem.Id));
                }
                return 0;
            }
            catch (CosmosException ex)
            {
                // ... obsługa błędów bez zmian
                return 1;
            }
        }

        private string? FindDifferences(ApartmentObject oldObj, ApartmentObject newObj)
        {
            if (oldObj == null || newObj == null)
            {
                return "One or both objects are null, cannot compare.";
            }
            
            var differences = new StringBuilder();
            var properties = typeof(ApartmentObject).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var oldValue = property.GetValue(oldObj);
                var newValue = property.GetValue(newObj);

                if (!Equals(oldValue, newValue))
                {
                    differences.AppendLine($"  - Property '{property.Name}': Old='{oldValue ?? "null"}', New='{newValue ?? "null"}'");
                }
            }

            return differences.Length > 0 ? differences.ToString() : null;
        }
    }
}