using Microsoft.EntityFrameworkCore;
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
       
        //  private const string ContainerName = "ApartmentInfo";
        /// <summary>
        ///        private const string PartitionKey = "/partitionKey";
        /// </summary>
        //    private const string ApartmentsPartitionKeyValue = "rentoom-apartments-list";
        //  private const string AmenitiesPartitionKeyValue = "rentoom-apartments-amenities-list";
        private ILogger<ApartmentRepository> _logger;
        private PostgresBookingDatabase _postgresBookingDatabase;
        private IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;

        public ApartmentRepository(PostgresBookingDatabase postgresBookingDatabase, IDbContextFactory<PostgresBookingDbContext> dbContextFactory,  IConfiguration configuration, ILogger<ApartmentRepository> logger)
        {
            _postgresBookingDatabase = postgresBookingDatabase;
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        public async Task<long> GetApartmentCountAsync(ILogger? log = null)
        {
            return await _postgresBookingDatabase.GetApartmentCountAsync();
        }

        public async Task SaveApartmentsAsync(IEnumerable<ApartmentObject> apartments, ILogger log, CancellationToken cancellationToken = default)
        {
                await _postgresBookingDatabase.SaveApartmentsAsync(apartments, log, cancellationToken);
            
        }

        public async Task SaveApartmentAmenitiesAsync(IEnumerable<ApartmentAmenitiesDocument> apartmentAmenities, ILogger log, CancellationToken cancellationToken = default)
        {
           await _postgresBookingDatabase.SaveApartmentAmenitiesAsync(apartmentAmenities, log, cancellationToken);
        }

        public ApartmentObject? FindApartmentInPostgres(int apartmentId, CancellationToken cancellationToken = default)
        {
            using var context = _dbContextFactory.CreateDbContext();
            var apentity = context.ApartmentInfos.FirstOrDefault(a => a.Id == apartmentId) ?? throw new KeyNotFoundException("Apartament Not found");

            var apobj = JsonConvert.DeserializeObject<ApartmentObject>(apentity.Payload);
            return apobj;
        }

        public async Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(string? continuationToken, int pageSize)
        {
            if (pageSize <= 0) pageSize = 50;

            _logger?.LogInformation("QueryApartmentsAsync called. continuationToken={Token}, pageSize={PageSize}", continuationToken, pageSize);

            await using var context = _dbContextFactory.CreateDbContext();
            long totalCount = await context.ApartmentInfos.LongCountAsync();

            _logger?.LogDebug("Total apartments in DB: {TotalCount}", totalCount);

            // sortuj po Id od 1->N
            var query = context.ApartmentInfos
                .AsNoTracking()
                .OrderBy(a => a.Id)
                .AsQueryable();

            
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

            var entities = await query.Take(pageSize).ToListAsync();

            var items = new List<ApartmentObject>(entities.Count);

            foreach (var entity in entities)
            {
                try
                {
                  
                    var dto = JsonConvert.DeserializeObject<ApartmentObject>(entity.Payload);
                    if (dto != null)
                    {
                        items.Add(dto);
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

            // If we returned a full page, return continuation token = last entity id, otherwise null (end)
            string? nextToken = null;
            if (entities.Count == pageSize)
            {
                nextToken = entities.Last().Id.ToString();
            }

            _logger?.LogInformation("QueryApartmentsAsync returning {Count} items. NextToken={NextToken}", items.Count, nextToken);

            return new PagedResult<ApartmentObject>(items, nextToken, items.Count, totalCount);
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

            await using var context = _dbContextFactory.CreateDbContext();
            var ret = context.ApartmentInfos.AsNoTracking().ToList();
                List<ApartmentObject> apObjects = new();
                foreach(var r in ret)
                apObjects.Add(JsonConvert.DeserializeObject<ApartmentObject>(r.Payload));

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
            await using var context = _dbContextFactory.CreateDbContext();
            var amenities = await context.ApartmentAmenities
                                            .Where(a => amenityIdSet.Contains(a.Id))
                                            .Select(a => JsonConvert.DeserializeObject<ApartmentAmenitiesDocument>(a.Payload))
                                            .Where(doc => doc != null &&
                                                          doc.ApartmentId>0 &&
                                                          doc.Amenities != null &&
                                                          amenityIdSet.All(id => doc.Amenities.Any(x => x.Id == id)))
                                            .ToListAsync();

            var apartmentIds =  amenities.Select(doc => doc.ApartmentId).ToList();

            return apartmentIds;

        }


        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync(CancellationToken cancellationToken = default)
        {
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            return await _dbContext.DefinedAddons.AsNoTracking().ToListAsync(cancellationToken);
        }
    }



}
