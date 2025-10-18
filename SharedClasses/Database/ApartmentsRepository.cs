using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;

namespace RentoomBooking.SharedClasses.Database
{
    public class ApartmentRepository
    {
        private Container? _apartmentInfoContainer;
        private readonly Task _initializationTask;

        private const string ContainerName = "ApartmentInfo";
        private const string PartitionKey = "/partitionKey";
        private const string ApartmentsPartitionKeyValue = "rentoom-apartments-list";
        private const string AmenitiesPartitionKeyValue = "rentoom-apartments-amenities-list";
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
                    PartitionKey = new PartitionKey(ApartmentsPartitionKeyValue)
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
            apartmentList.ForEach(a => a.PartitionKey = ApartmentsPartitionKeyValue);


            log.LogInformation("Purging ApartmentInfo container before inserting new data.");

            try
            {
                long count = await GetApartmentCountAsync(log);
                log.LogInformation("{count} records to be purged", count);


                //await PurgeContainerAsync(log, cancellationToken);
                await PurgePartitionAsync(ApartmentsPartitionKeyValue, log, cancellationToken);
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

        public async Task SaveApartmentAmenitiesAsync(IEnumerable<ApartmentAmenitiesDocument> apartmentAmenities, ILogger log, CancellationToken cancellationToken = default)
        {
            if (apartmentAmenities == null) throw new ArgumentNullException(nameof(apartmentAmenities));
            if (log == null) throw new ArgumentNullException(nameof(log));

            await _initializationTask;

            if (_apartmentInfoContainer == null)
            {
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");
            }

            var amenitiesList = apartmentAmenities
                .Where(document => document != null)
                .Select(document =>
                {
                    document.PartitionKey = AmenitiesPartitionKeyValue;

                    if (string.IsNullOrWhiteSpace(document.Id))
                    {
                        document.Id = document.ApartmentId ?? Guid.NewGuid().ToString("N");
                    }

                    if (string.IsNullOrWhiteSpace(document.ApartmentId))
                    {
                        document.ApartmentId = document.Id;
                    }

                    document.Amenities ??= new List<ObjectAmenity>();

                    return document;
                })
                .ToList();

            log.LogInformation("Saving {count} apartment amenities documents to partition {Partition}", amenitiesList.Count, AmenitiesPartitionKeyValue);

            await PurgePartitionAsync(AmenitiesPartitionKeyValue, log, cancellationToken);

            foreach (var amenityDocument in amenitiesList)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _apartmentInfoContainer.UpsertItemAsync(
                    amenityDocument,
                    new PartitionKey(AmenitiesPartitionKeyValue),
                    cancellationToken: cancellationToken);
            }

            log.LogInformation("Apartment amenities saved successfully to partition {Partition}", AmenitiesPartitionKeyValue);
        }

       // private Task PurgeAll(ILogger? log = null, CancellationToken cancellationToken = default) =>
       //     PurgePartitionAsync(PartitionKeyValue, log, cancellationToken);

        private async Task PurgePartitionAsync(string partitionKeyValue, ILogger? log = null, CancellationToken cancellationToken = default)
        {
            var logger = log ?? _logger;
            logger?.LogInformation("Purging logical partition '{pk}' in ApartmentRepository...", partitionKeyValue);

            if (_apartmentInfoContainer is null)
            {
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");
            }

            try
            {
                ResponseMessage deleteResponse = await _apartmentInfoContainer.DeleteAllItemsByPartitionKeyStreamAsync(
                    new PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken);

                if (deleteResponse.IsSuccessStatusCode)
                {
                    logger?.LogInformation("Partition '{pk}' delete started successfully.", partitionKeyValue);
                }
                else if (deleteResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    logger?.LogInformation("Partition '{pk}' not found during purge request.", partitionKeyValue);
                }
                else
                {
                    logger?.LogWarning("Partition delete returned {code}: {msg}", deleteResponse.StatusCode, deleteResponse.ErrorMessage);
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                logger?.LogInformation("Partition '{pk}' does not exist yet.", partitionKeyValue);
            }
        }

        public async Task BulkCreateItemsAsync(List<ApartmentObject> items, ILogger log)
        {
            await _initializationTask;
            if (_apartmentInfoContainer == null) throw new InvalidOperationException("Apartment container not initialized.");
           // await PurgeAll();

            int itemsPerBatch = 25;
            int totalItemsCreated = 0;
            log.LogInformation($"Starting bulk create for a total of {items.Count} items.");

            foreach (var a in items) a.PartitionKey = ApartmentsPartitionKeyValue;

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
                        .CreateItemAsync(item, new PartitionKey(ApartmentsPartitionKeyValue), cancellationToken: ct);

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
                    partitionKey: new PartitionKey(ApartmentsPartitionKeyValue),
                    cancellationToken: cancellationToken);

                var apartment = response.Resource;

                return apartment;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Apartment with id {ApartmentId} not found in partition {PK}.", apartmentId, ApartmentsPartitionKeyValue);
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
                PartitionKey = new PartitionKey(ApartmentsPartitionKeyValue)
            };

            long totalCount = await GetApartmentCountAsync();

            var it = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(qd, continuationToken, opts);
            if (!it.HasMoreResults)
                return new PagedResult<ApartmentObject>(Array.Empty<ApartmentObject>(), null, 0, totalCount);

            var page = await it.ReadNextAsync();
            return new PagedResult<ApartmentObject>(page.ToList(), page.ContinuationToken, page.Count, totalCount);
        }


        public async Task<List<ApartmentObject>> GetApartmentsByFilterAsync_old(ApartmentQueryFilter apartmentFilter, CancellationToken cancellationToken = default)
        {
            var apartmentIds = apartmentFilter.ApartmentIds;
        //    if (apartmentIds == null) throw new ArgumentNullException(nameof(apartmentIds));

            await _initializationTask;

            if (_apartmentInfoContainer == null)
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");

            
                var amenityApartmentIds = await GetApartmentIdsByAmenitiesAsync(apartmentFilter.ApartmentAmenityIds, cancellationToken);


            var distinctIds = amenityApartmentIds.Distinct().Select(id => id.ToString()).ToList();

            if (distinctIds.Count == 0)
            {
                return new List<ApartmentObject>();
            }

            var parameterNames = distinctIds
                .Select((_, index) => $"@id{index}")
                .ToList();

            var queryText = $"SELECT * FROM c WHERE c.id IN ({string.Join(", ", parameterNames)})";
            var queryDefinition = new QueryDefinition(queryText);

            for (int i = 0; i < distinctIds.Count; i++)
            {
                queryDefinition.WithParameter(parameterNames[i], distinctIds[i]);
            }

            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(ApartmentsPartitionKeyValue)
            };

            var apartments = new List<ApartmentObject>();
            using var iterator = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(queryDefinition, requestOptions: requestOptions);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                apartments.AddRange(response.ToList());
            }

            return apartments;
        }

        public async Task<List<ApartmentObject>?> GetApartmentsByFilterAsync(ApartmentQueryFilter apartmentFilter, CancellationToken cancellationToken = default)
        {
            var apartmentIds = apartmentFilter.ApartmentIds;
          //  if (apartmentIds == null) throw new ArgumentNullException(nameof(apartmentIds));

            await _initializationTask;

            if (_apartmentInfoContainer == null)
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");


            var ids = (apartmentFilter.ApartmentIds ?? Enumerable.Empty<int>()).ToList();

            if (apartmentFilter.ApartmentAmenityIds != null && apartmentFilter.ApartmentAmenityIds.Any())
            {
                var amenityApartmentIds = await GetApartmentIdsByAmenitiesAsync(apartmentFilter.ApartmentAmenityIds, cancellationToken);

                ids.AddRange(amenityApartmentIds);
            }


            var idStrings = ids.Distinct().Select(i => i.ToString()).ToList();

            var regions = (apartmentFilter.ApartmentObjectLocalizationItemRegionNames ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var where = new List<string>();
            if (ids.Count > 0) where.Add("ARRAY_CONTAINS(@idStrings, c.id)");
            if (regions.Count > 0) where.Add("ARRAY_CONTAINS(@regions, c.objectLocation.localizationItem.region)");

            if (where.Count == 0)
                return new List<ApartmentObject>();

            var queryText = $"SELECT * FROM c WHERE {string.Join(" AND ", where)}";
               
            var queryDefinition = new QueryDefinition(queryText);
            if (idStrings.Count > 0) queryDefinition.WithParameter("@idStrings", idStrings);
            if (regions.Count > 0) queryDefinition.WithParameter("@regions", regions);


            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(ApartmentsPartitionKeyValue)
            };

            var apartments = new List<ApartmentObject>();
            using var iterator = _apartmentInfoContainer.GetItemQueryIterator<ApartmentObject>(queryDefinition, requestOptions: requestOptions);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                apartments.AddRange(response.ToList());
            }

            return apartments;
        }

        public async Task<List<ApartmentAmenitiesDocument>> GetAllApartmentAmenitiesAsync(
    CancellationToken cancellationToken = default)
        {
            await _initializationTask;

            if (_apartmentInfoContainer is null)
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");

            // Query everything in the amenities partition
            var qd = new QueryDefinition("SELECT * FROM c");
            var opts = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(AmenitiesPartitionKeyValue)
            };

            var results = new List<ApartmentAmenitiesDocument>();

            using var it = _apartmentInfoContainer.GetItemQueryIterator<ApartmentAmenitiesDocument>(
                qd,
                requestOptions: opts);

            while (it.HasMoreResults)
            {
                var page = await it.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }

            return results;
        }


        private async Task<List<int>> GetApartmentIdsByAmenitiesAsync(IEnumerable<int> amenityIds, CancellationToken cancellationToken)
        {
            if (_apartmentInfoContainer == null)
            {
                throw new InvalidOperationException("ApartmentInfo container is not initialized.");
            }
            var all = await GetAllApartmentAmenitiesAsync();

            var amenityIdSet = amenityIds?
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet() ?? new HashSet<int>();

            if (amenityIdSet.Count == 0)
            {
                return new List<int>();
            }

            var queryDefinition = new QueryDefinition("SELECT * FROM c");
            var requestOptions = new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(AmenitiesPartitionKeyValue)
            };

            var apartmentIds = new List<int>();

            using var iterator = _apartmentInfoContainer.GetItemQueryIterator<ApartmentAmenitiesDocument>(queryDefinition, requestOptions: requestOptions);

            foreach (var amenityDoc in all)
            {
                if (amenityDoc?.Amenities == null || string.IsNullOrWhiteSpace(amenityDoc.ApartmentId))
                {
                    continue;
                }

                var documentAmenityIds = amenityDoc.Amenities
                       .Where(amenity => amenity != null)
                       .Select(amenity => amenity.Id)
                       .ToHashSet();

                if (!amenityIdSet.All(documentAmenityIds.Contains))
                {
                    continue;
                }

                if (int.TryParse(amenityDoc.ApartmentId, out var parsedId))
                {
                    apartmentIds.Add(parsedId);
                }

            }

            return apartmentIds;

        }


    }
}