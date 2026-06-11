using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Xml.Linq;

namespace RentoomBooking.SharedClasses.Database
{
    public class ApartmentRepository
    {
        private static long _apartmentCacheVersion;
        private static long _apartmentAmenitiesCacheVersion;
        private static readonly TimeSpan ApartmentCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ApartmentCatalogCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ApartmentAmenitiesCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DefinedAddonsCacheTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan DefinedAmenitiesCacheTtl = TimeSpan.FromMinutes(30);
       
        //  private const string ContainerName = "ApartmentInfo";
        /// <summary>
        ///        private const string PartitionKey = "/partitionKey";
        /// </summary>
        //    private const string ApartmentsPartitionKeyValue = "rentoom-apartments-list";
        //  private const string AmenitiesPartitionKeyValue = "rentoom-apartments-amenities-list";
        private ILogger<ApartmentRepository> _logger;
        private PostgresBookingDatabase _postgresBookingDatabase;
        private IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly IMemoryCache _memoryCache;

        public ApartmentRepository(PostgresBookingDatabase postgresBookingDatabase, IDbContextFactory<PostgresBookingDbContext> dbContextFactory, IConfiguration configuration, ILogger<ApartmentRepository> logger, IMemoryCache memoryCache)
        {
            _postgresBookingDatabase = postgresBookingDatabase;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public async Task<long> GetApartmentCountAsync(ILogger? log = null)
        {
            var apartments = await GetActiveApartmentsSnapshotAsync();
            return apartments.Count;
        }

        public async Task SaveApartmentsAsync(IEnumerable<ApartmentObject> apartments, ILogger log, CancellationToken cancellationToken = default)
        {
                await _postgresBookingDatabase.SaveApartmentsAsync(apartments, log, cancellationToken);
                InvalidateApartmentCache();
            
        }

        public async Task SaveApartmentAmenitiesAsync(IEnumerable<ApartmentAmenitiesDocument> apartmentAmenities, ILogger log, CancellationToken cancellationToken = default)
        {
           await _postgresBookingDatabase.SaveApartmentAmenitiesAsync(apartmentAmenities, log, cancellationToken);
           InvalidateApartmentAmenitiesCache();
        }

        public ApartmentObject? FindApartmentInPostgres(int apartmentId, CancellationToken cancellationToken = default)
        {
            var cacheKey = BuildApartmentCacheKey(apartmentId);
            if (_memoryCache.TryGetValue(cacheKey, out ApartmentObject? cachedApartment))
            {
                return cachedApartment;
            }

            using var context = _dbContextFactory.CreateDbContext();
            var apentity = context.ApartmentInfos.AsNoTracking().FirstOrDefault(a => a.Id == apartmentId && !a.IsArchived) ?? throw new KeyNotFoundException("Apartament Not found");

            var apobj = JsonConvert.DeserializeObject<ApartmentObject>(apentity.Payload);
            if (apobj is not null)
            {
                _memoryCache.Set(cacheKey, apobj, ApartmentCacheTtl);
            }

            return apobj;
        }

        public ApartmentObject? FindApartmentByItemId(int objectItemId, CancellationToken cancellationToken = default)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var entities = context.ApartmentInfos.AsNoTracking().ToList();

            foreach (var entity in entities)
            {
                ApartmentObject? apartment = null;
                try
                {
                    apartment = JsonConvert.DeserializeObject<ApartmentObject>(entity.Payload);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to deserialize ApartmentInfo payload for id {Id}", entity.Id);
                }

                if (apartment?.Items is null)
                {
                    continue;
                }

                if (apartment.Items.Any(item => item.Id == objectItemId))
                {
                    return apartment;
                }
            }

            return null;
        }

        public async Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(string? continuationToken, int pageSize)
        {
            if (pageSize <= 0) pageSize = 50;

            _logger?.LogInformation("QueryApartmentsAsync called. continuationToken={Token}, pageSize={PageSize}", continuationToken, pageSize);

            var apartments = await GetActiveApartmentsSnapshotAsync();
            long totalCount = apartments.Count;

            _logger?.LogDebug("Total apartments in DB: {TotalCount}", totalCount);

            var query = apartments.AsEnumerable();

            
            // continuation token , jeśli jest to jest to ostatnie pobrane Id (posortowane) i kolejny page zaczyna się od większych ostatniego id. 
            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                if (int.TryParse(continuationToken, out var lastId))
                {
                    query = query.Where(a => a.Id > lastId);
                    _logger?.LogDebug("Applied continuation filter: Id > {LastId}", lastId);
                }
                else
                {
                    _logger?.LogWarning("Invalid continuation token '{Token}' - expected integer Id. Ignoring token.", continuationToken);
                }
            }

            var items = query.Take(pageSize).ToList();

            // If we returned a full page, return continuation token = last entity id, otherwise null (end)
            string? nextToken = null;
            if (items.Count == pageSize)
            {
                nextToken = items.Last().Id.ToString();
            }

            _logger?.LogInformation("QueryApartmentsAsync returning {Count} items. NextToken={NextToken}", items.Count, nextToken);

            return new PagedResult<ApartmentObject>(items, nextToken, items.Count, totalCount);
        }

        private static string BuildApartmentCacheKey(int apartmentId)
        {
            return $"apartments:by-id:v{Volatile.Read(ref _apartmentCacheVersion)}:{apartmentId}";
        }

        private static void InvalidateApartmentCache()
        {
            Interlocked.Increment(ref _apartmentCacheVersion);
        }


      
        public async Task<List<ApartmentObject>?> GetApartmentsByFilterAsync(ApartmentQueryFilter apartmentFilter, CancellationToken cancellationToken = default)
        {
            var apartmentIds = apartmentFilter.ApartmentIds;
         
            var ids = (apartmentFilter.ApartmentIds ?? Enumerable.Empty<int>()).ToList();

            if (apartmentFilter.ApartmentAmenityIds != null && apartmentFilter.ApartmentAmenityIds.Any())
            {
                var amenityApartmentIds = await GetApartmentIdsByAmenitiesAsync(apartmentFilter.ApartmentAmenityIds, cancellationToken);

                ids.AddRange(amenityApartmentIds);
            }


            var regions = (apartmentFilter.ApartmentObjectLocalizationItemRegionNames ?? Enumerable.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var apObjects = await GetActiveApartmentsSnapshotAsync(cancellationToken);

            var filtered = apObjects.AsQueryable();

            if (ids.Count != 0)
            {
                filtered = filtered.Where(a => ids.Contains(a.Id));
            }

            if (regions.Count != 0)
            {
                filtered = filtered.Where(a => regions.Contains(a.ObjectLocation.LocalizationItem.Region));
            }

           
            return filtered.ToList(); 
        }

      
        private async Task<List<int>> GetApartmentIdsByAmenitiesAsync(IEnumerable<int> amenityIds, CancellationToken cancellationToken)
        {
            var amenityIdSet = amenityIds?
                .Where(id => id > 0)
                .Distinct()
                .ToHashSet() ?? new HashSet<int>();

            if (amenityIdSet.Count == 0)
            {
                return new List<int>();
            }

            var allAmenitiesDocuments = await GetApartmentAmenitiesSnapshotAsync(cancellationToken);

            var apartmentIds = allAmenitiesDocuments
                .Where(doc => doc != null && 
                              doc.ApartmentId > 0 && 
                              doc.Amenities != null &&
                              amenityIdSet.All(id => doc.Amenities.Any(x => x.Id == id)))
                .Select(doc => doc.ApartmentId)
                .Distinct()
                .ToList();

            return apartmentIds;
        }


        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync(CancellationToken cancellationToken = default)
        {
            var cachedAddons = await _memoryCache.GetOrCreateAsync(BuildDefinedAddonsCacheKey(), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = DefinedAddonsCacheTtl;

                await using var _dbContext = _dbContextFactory.CreateDbContext();
                return await _dbContext.DefinedAddons.AsNoTracking().ToListAsync(cancellationToken);
            });

            return cachedAddons ?? [];
        }

        public async Task<List<ApartmentDefinedAmenityDto>> GetDefinedAmenitiesAsync(string? lang, CancellationToken cancellationToken = default)
        {
            var normalizedLang = NormalizeLanguage(lang);
            var cacheKey = BuildDefinedAmenitiesCacheKey(normalizedLang);

            var cachedAmenities = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = DefinedAmenitiesCacheTtl;

                await using var context = _dbContextFactory.CreateDbContext();
                return await context.DefinedAmenities
                    .AsNoTracking()
                    .Where(x => x.Lang == normalizedLang)
                    .OrderBy(x => x.AmenityTypeName)
                    .ThenBy(x => x.AmenityName)
                    .Select(x => new ApartmentDefinedAmenityDto
                    {
                        Id = x.Id,
                        AmenityId = x.AmenityId,
                        AmenityTypeName = x.AmenityTypeName,
                        AmenityName = x.AmenityName,
                        Lang = x.Lang,
                        IconSource = x.IconSource
                    })
                    .ToListAsync(cancellationToken);
            });

            return cachedAmenities ?? [];
        }

        public async Task<ApartmentAmenitiesDocument?> GetApartmentAmenitiesDocumentAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            if (apartmentId <= 0)
            {
                return null;
            }

            var allAmenitiesDocuments = await GetApartmentAmenitiesSnapshotAsync(cancellationToken);
            return allAmenitiesDocuments.FirstOrDefault(doc => doc.ApartmentId == apartmentId || doc.Id == apartmentId);
        }

        public long GetApartmentAmenitiesCacheVersion()
        {
            return Volatile.Read(ref _apartmentAmenitiesCacheVersion);
        }

        private async Task<List<ApartmentObject>> GetActiveApartmentsSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = BuildActiveApartmentsCacheKey();
            var cachedApartments = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ApartmentCatalogCacheTtl;

                await using var context = _dbContextFactory.CreateDbContext();
                var entities = await context.ApartmentInfos
                    .AsNoTracking()
                    .Where(a => !a.IsArchived)
                    .OrderBy(a => a.Id)
                    .ToListAsync(cancellationToken);

                var apartments = new List<ApartmentObject>(entities.Count);

                foreach (var entity in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var apartment = JsonConvert.DeserializeObject<ApartmentObject>(entity.Payload);
                        if (apartment != null)
                        {
                            apartments.Add(apartment);
                        }
                        else
                        {
                            _logger?.LogWarning("Deserialization of ApartmentInfo payload returned null for id {Id}", entity.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to deserialize ApartmentInfo payload for id {Id}", entity.Id);
                    }
                }

                return apartments;
            });

            return cachedApartments ?? [];
        }

        private async Task<List<ApartmentAmenitiesDocument>> GetApartmentAmenitiesSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = BuildApartmentAmenitiesCacheKey();
            var cachedAmenities = await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = ApartmentAmenitiesCacheTtl;

                await using var context = _dbContextFactory.CreateDbContext();
                var entities = await context.ApartmentAmenities.AsNoTracking().ToListAsync(cancellationToken);
                var documents = new List<ApartmentAmenitiesDocument>(entities.Count);

                foreach (var entity in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var document = JsonConvert.DeserializeObject<ApartmentAmenitiesDocument>(entity.Payload);
                        if (document != null)
                        {
                            documents.Add(document);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to deserialize ApartmentAmenities payload for id {Id}", entity.Id);
                    }
                }

                return documents;
            });

            return cachedAmenities ?? [];
        }

        private static string BuildActiveApartmentsCacheKey()
        {
            return $"apartments:all:v{Volatile.Read(ref _apartmentCacheVersion)}";
        }

        private static string BuildApartmentAmenitiesCacheKey()
        {
            return $"apartments:amenities:v{Volatile.Read(ref _apartmentAmenitiesCacheVersion)}";
        }

        private static string BuildDefinedAddonsCacheKey()
        {
            return "apartments:defined-addons";
        }

        private static string BuildDefinedAmenitiesCacheKey(string lang)
        {
            return $"apartments:defined-amenities:{lang}";
        }

        private static void InvalidateApartmentAmenitiesCache()
        {
            Interlocked.Increment(ref _apartmentAmenitiesCacheVersion);
        }

        private static string NormalizeLanguage(string? lang)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return "pl";
            }

            return lang.Trim().ToLowerInvariant();
        }
    }



}
