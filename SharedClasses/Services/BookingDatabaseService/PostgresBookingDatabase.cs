using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.BookingDatabaseService
{

    public class PostgresBookingDatabase
    {
        private readonly ILogger<PostgresBookingDatabase> _logger;
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly Task _initializationTask;

        private const string HashDocumentId = "all-object-hashes";

        public PostgresBookingDatabase(IDbContextFactory<PostgresBookingDbContext> dbContextFactory, ILogger<PostgresBookingDatabase> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_initializationTask = EnsureCreatedAsync();
        }
        private async Task EnsureCreatedAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();
        }
        public async Task<List<SearchFilterDocument>> GetAllSearchFiltersAsync(ILogger log, CancellationToken cancellationToken = default)
        {
            if (log is null) throw new ArgumentNullException(nameof(log));

           // // await _initializationTask;
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entities = await _dbContext.SearchFilters.AsNoTracking().ToListAsync(cancellationToken);
            var results = new List<SearchFilterDocument>(entities.Count);

            foreach (var entity in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var document = JsonConvert.DeserializeObject<SearchFilterDocument>(entity.Payload);

                    if (document is not null)
                    {
                        results.Add(document);
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to deserialize search filters payload for group {GroupName}.", entity.FilterGroupName);
                }
            }

            return results;
        }

        public async Task<long> GetApartmentCountAsync(CancellationToken cancellationToken = default)
        {
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            return await _dbContext.ApartmentInfos.Where(ap=>!ap.IsArchived).LongCountAsync(cancellationToken);
        }

        public async Task SaveApartmentsAsync(IEnumerable<ApartmentObject> apartments, ILogger log, CancellationToken cancellationToken = default)
        {
            if (apartments is null) throw new ArgumentNullException(nameof(apartments));
            if (log is null) throw new ArgumentNullException(nameof(log));

            var list = apartments.ToList();
            var ids = list.Select(a => a.Id).ToList();
            await using var _dbContext = _dbContextFactory.CreateDbContext();

            var existing = await _dbContext.ApartmentInfos
                .Where(ai => ids.Contains(ai.Id))
                .ToDictionaryAsync(ai => ai.Id, cancellationToken);

            foreach (var apartment in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (existing.TryGetValue(apartment.Id, out var entity))
                {
                    entity.Payload = JsonConvert.SerializeObject(apartment);
                    entity.UpdatedAt = DateTime.UtcNow;
                    _dbContext.ApartmentInfos.Update(entity);
                }
                else
                {
                    _dbContext.ApartmentInfos.Add(new ApartmentInfoEntity
                    {
                        Id = apartment.Id,
                        Payload = JsonConvert.SerializeObject(apartment),
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            var toArchive = await _dbContext.ApartmentInfos
                                    .Where(ai => !ids.Contains(ai.Id))
                                    .ExecuteUpdateAsync(ap =>ap.SetProperty(a => a.IsArchived, true));
                                    
                                    

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Saved {Count} apartments to PostgreSQL table {Table}.", list.Count, "apartment_info");
        }

        public async Task SaveApartmentAmenitiesAsync(IEnumerable<ApartmentAmenitiesDocument> amenities, ILogger log, CancellationToken cancellationToken = default)
        {
            if (amenities is null) throw new ArgumentNullException(nameof(amenities));
            if (log is null) throw new ArgumentNullException(nameof(log));

            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var docs = amenities.ToList();
            var ids = docs.Select(a => a.Id).ToList();
            var existing = await _dbContext.ApartmentAmenities
                .Where(a => ids.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            foreach (var amenityDoc in docs)
            {
               
               // var apartmentId = amenityDoc.ApartmentId ?? docId;
                var payload = JsonConvert.SerializeObject(amenityDoc);

                if (existing.TryGetValue(amenityDoc.Id, out var entity))
                {
               //     entity.ApartmentId = apartmentId;
                    entity.Payload = payload;
                    entity.UpdatedAt = DateTime.UtcNow;
                    _dbContext.ApartmentAmenities.Update(entity);
                }
                else
                {
                    _dbContext.ApartmentAmenities.Add(new ApartmentAmenityEntity
                    {
                        Id = amenityDoc.Id,
                 //       ApartmentId = apartmentId,
                        Payload = payload,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Saved {Count} amenities documents to PostgreSQL table {Table}.", docs.Count, "apartment_amenities");
        }

        public async Task SaveSearchFiltersAsync(Dictionary<string, List<SearchFilter>> filtersDictionary, string filterGroupName, ILogger log, CancellationToken cancellationToken = default)
        {
            if (filtersDictionary is null) throw new ArgumentNullException(nameof(filtersDictionary));
            if (string.IsNullOrWhiteSpace(filterGroupName)) throw new ArgumentNullException(nameof(filterGroupName));
            if (log is null) throw new ArgumentNullException(nameof(log));

            // await _initializationTask;

            var document = new SearchFilterDocument
            {
                id = filterGroupName,
                filtersDictionary = filtersDictionary
            };

            var payload = JsonConvert.SerializeObject(document);
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entity = await _dbContext.SearchFilters.FirstOrDefaultAsync(s => s.FilterGroupName == filterGroupName, cancellationToken);

            if (entity is null)
            {
                _dbContext.SearchFilters.Add(new SearchFiltersEntity
                {
                    FilterGroupName = filterGroupName,
                    Payload = payload
                });
            }
            else
            {
                entity.Payload = payload;
                _dbContext.SearchFilters.Update(entity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Saved search filters for group {Group} to PostgreSQL table {Table}.", filterGroupName, "search_filters");
        }

        
        public async Task<RentoomReservation?> GetRentoomReservationByResTokenAsync(string resToken, ILogger log, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resToken)) throw new ArgumentNullException(nameof(resToken));
            if (log is null) throw new ArgumentNullException(nameof(log));

            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entity = await _dbContext.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.ResToken == resToken, cancellationToken);
            if (entity?.Payload is null)
            {
                log.LogWarning("Reservation with token {Token} not found in PostgreSQL.", resToken);
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<RentoomReservation>(entity.Payload);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize reservation {Token} from PostgreSQL.", resToken);
                return null;
            }
        }

        public async Task<string?> SaveReservationJsonAsync(Reservation payloadReservation, ILogger log, string? existingResToken = null, CancellationToken cancellationToken = default)
        {
            if (payloadReservation is null) throw new ArgumentNullException(nameof(payloadReservation));
            if (log is null) throw new ArgumentNullException(nameof(log));
            
            var resToken = existingResToken;
            
            if (existingResToken is null)
                resToken = Guid.NewGuid().ToString("N");
            
            // await _initializationTask;
            
            var document = new RentoomReservation
            {
                Id = payloadReservation.id,
                ResToken = resToken,
                Reservation = payloadReservation,

            };
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var payload = JsonConvert.SerializeObject(document);
            var existingEntity = await _dbContext.Reservations
                .FirstOrDefaultAsync(r => r.ResToken == resToken, cancellationToken);

            if (existingEntity is null)
            {
                _dbContext.Reservations.Add(new ReservationEntity
                {
                    ResToken = resToken,
                    ReservationId = payloadReservation.id,
                    Payload = payload,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingEntity.ReservationId = payloadReservation.id;
                existingEntity.Payload = payload;
                existingEntity.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                log.LogInformation("Saved reservation {SourceReservationId} to PostgreSQL with token {Token}.", payloadReservation.id, resToken);
                return resToken;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to save reservation {Token} to PostgreSQL.", resToken);
                return null;
            }
        }

        public async Task<bool> UpdateReservationJsonAsync(Reservation payloadReservation, ILogger log, CancellationToken cancellationToken = default)
        {
            if (payloadReservation is null) throw new ArgumentNullException(nameof(payloadReservation));
            if (log is null) throw new ArgumentNullException(nameof(log));

            // await _initializationTask;
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entity = await _dbContext.Reservations
                .Where(r => r.ReservationId == payloadReservation.id)
                .OrderByDescending(r => r.UpdatedAt)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity is null)
            {
                log.LogWarning("Reservation with id {Id} not found in PostgreSQL, update skipped.", payloadReservation.id);
                return false;
            }

            var document = DeserializeReservationDocument(entity.Payload, entity.ResToken, payloadReservation, log);
            entity.Payload = JsonConvert.SerializeObject(document);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.ReservationId = payloadReservation.id;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to update reservation {Id} in PostgreSQL.", payloadReservation.id);
                return false;
            }
        }

        public async Task<Reservation?> GetReservationByIdAsync(int reservationId, ILogger log, CancellationToken cancellationToken = default)
        {
            if (log is null) throw new ArgumentNullException(nameof(log));

            // await _initializationTask;
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entity = await _dbContext.Reservations.AsNoTracking()
                .OrderByDescending(r => r.UpdatedAt)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId, cancellationToken);
            if (entity?.Payload is null)
            {
                log.LogWarning("Reservation with id {Id} not found in PostgreSQL.", reservationId);
                return null;
            }

            try
            {
                var reservation = JsonConvert.DeserializeObject<RentoomReservation>(entity.Payload);
                return reservation?.Reservation;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize reservation {Id} from PostgreSQL.", reservationId);
                return null;
            }
        }

        public async Task<Reservation?> GetReservationTemplateAsync(string templateKey, ILogger log, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(templateKey)) throw new ArgumentNullException(nameof(templateKey));
            if (log is null) throw new ArgumentNullException(nameof(log));

            // await _initializationTask;
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var entity = await _dbContext.ReservationTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateKey == templateKey, cancellationToken);
            if (entity?.Payload is null)
            {
                log.LogWarning("Reservation template {TemplateKey} not found in PostgreSQL.", templateKey);
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<Reservation>(entity.Payload);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize reservation template {TemplateKey} from PostgreSQL.", templateKey);
                return null;
            }
        }

        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync(CancellationToken cancellationToken = default)
        {
            // await _initializationTask;
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            return await _dbContext.DefinedAddons.AsNoTracking().ToListAsync(cancellationToken);
        }

        internal async void UpdateReservationStatusInWorkflow(int reservationId, string status)
        {
            await using var _dbContext = _dbContextFactory.CreateDbContext();
            var res = await _dbContext.ReservationRecords.FirstOrDefaultAsync(r => r.IdoReservationId == reservationId);
            if (res is null)
            {
                return;
            }

            res.IdoStatus = status;
            await _dbContext.SaveChangesAsync();
        }

        private static RentoomReservation DeserializeReservationDocument(string payload, string resToken, Reservation payloadReservation, ILogger log)
        {
            try
            {
                var current = JsonConvert.DeserializeObject<RentoomReservation>(payload);
                if (current is not null)
                {
                    current.Id = payloadReservation.id;
                    current.ResToken = string.IsNullOrWhiteSpace(current.ResToken) ? resToken : current.ResToken;
                    current.Reservation = payloadReservation;
                    return current;
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Failed to deserialize existing reservation payload for token {Token}. Rebuilding document.", resToken);
            }

            return new RentoomReservation
            {
                Id = payloadReservation.id,
                ResToken = resToken,
                Reservation = payloadReservation
            };
        }
    }

}

