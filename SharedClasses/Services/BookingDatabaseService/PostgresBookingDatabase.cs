using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.BookingDatabaseService
{

    public class PostgresBookingDatabase
    {
        private readonly ILogger<PostgresBookingDatabase> _logger;
        private readonly PostgresBookingDbContext _dbContext;
        private readonly Task _initializationTask;

        private const string HashDocumentId = "all-object-hashes";

        public PostgresBookingDatabase(PostgresBookingDbContext dbContext, ILogger<PostgresBookingDatabase> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _initializationTask = _dbContext.Database.EnsureCreatedAsync();
        }

        public async Task<bool> HasRecordsAsync(CancellationToken cancellationToken = default)
        {
          //  await _initializationTask;
            return await _dbContext.ApartmentInfos.AnyAsync(cancellationToken);
        }

        public async Task<long> GetApartmentCountAsync(CancellationToken cancellationToken = default)
        {
           // await _initializationTask;
            return await _dbContext.ApartmentInfos.LongCountAsync(cancellationToken);
        }

        public async Task SaveApartmentsAsync(IEnumerable<ApartmentObject> apartments, ILogger log, CancellationToken cancellationToken = default)
        {
            if (apartments is null) throw new ArgumentNullException(nameof(apartments));
            if (log is null) throw new ArgumentNullException(nameof(log));

          //  await _initializationTask;
            var list = apartments.ToList();
            var ids = list.Select(a => a.Id).ToList();
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

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Saved {Count} apartments to PostgreSQL table {Table}.", list.Count, "apartment_info");
        }

        public async Task SaveApartmentAmenitiesAsync(IEnumerable<ApartmentAmenitiesDocument> amenities, ILogger log, CancellationToken cancellationToken = default)
        {
            if (amenities is null) throw new ArgumentNullException(nameof(amenities));
            if (log is null) throw new ArgumentNullException(nameof(log));

          //  await _initializationTask;
            var docs = amenities.ToList();
            var ids = docs.Select(a => string.IsNullOrWhiteSpace(a.Id) ? a.ApartmentId ?? string.Empty : a.Id!).ToList();
            var existing = await _dbContext.ApartmentAmenities
                .Where(a => ids.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            foreach (var amenityDoc in docs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var docId = string.IsNullOrWhiteSpace(amenityDoc.Id) ? amenityDoc.ApartmentId ?? Guid.NewGuid().ToString("N") : amenityDoc.Id!;
                var apartmentId = amenityDoc.ApartmentId ?? docId;
                var payload = JsonConvert.SerializeObject(amenityDoc);

                if (existing.TryGetValue(docId, out var entity))
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
                        Id = docId,
                 //       ApartmentId = apartmentId,
                        Payload = payload,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Saved {Count} amenities documents to PostgreSQL table {Table}.", docs.Count, "apartment_amenities");
        }

        public async Task<List<ItemHash>> GetExistingHashesAsync(ILogger log, CancellationToken cancellationToken = default)
        {
            if (log is null) throw new ArgumentNullException(nameof(log));
           // await _initializationTask;

            var entity = await _dbContext.ApartmentHashes.FirstOrDefaultAsync(h => h.Id == HashDocumentId, cancellationToken);
            if (entity?.Payload is null)
            {
                log.LogInformation("Hash document not found in PostgreSQL, returning empty list.");
                return new List<ItemHash>();
            }

            try
            {
                var hashDoc = JsonConvert.DeserializeObject<ApartmentObjectHash>(entity.Payload);
                return hashDoc?.Hashes ?? new List<ItemHash>();
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize hash document from PostgreSQL.");
                return new List<ItemHash>();
            }
        }

        public async Task UpdateHashesDocumentAsync(List<ItemHash> hashes, ILogger log, CancellationToken cancellationToken = default)
        {
            if (hashes is null) throw new ArgumentNullException(nameof(hashes));
            if (log is null) throw new ArgumentNullException(nameof(log));

            await _initializationTask;

            var hashDoc = new ApartmentObjectHash
            {
                Hashes = hashes,
                lastUpdated = DateTime.UtcNow
            };

            var payload = JsonConvert.SerializeObject(hashDoc);
            var entity = await _dbContext.ApartmentHashes.FirstOrDefaultAsync(h => h.Id == HashDocumentId, cancellationToken);

            if (entity is null)
            {
                _dbContext.ApartmentHashes.Add(new ApartmentHashEntity
                {
                    Id = HashDocumentId,
                    Payload = payload,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                entity.Payload = payload;
                entity.UpdatedAt = DateTime.UtcNow;
                _dbContext.ApartmentHashes.Update(entity);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            log.LogInformation("Updated hash document in PostgreSQL table {Table}.", "apartment_hashes");
        }

        public async Task<RentoomReservation?> GetRentoomReservationByResTokenAsync(string resToken, ILogger log, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resToken)) throw new ArgumentNullException(nameof(resToken));
            if (log is null) throw new ArgumentNullException(nameof(log));

            await _initializationTask;
            var entity = await _dbContext.Reservations.FirstOrDefaultAsync(r => r.ResToken == resToken, cancellationToken);
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

        public async Task<string?> SaveReservationJsonAsync(Reservation payloadReservation, ILogger log, CancellationToken cancellationToken = default)
        {
            if (payloadReservation is null) throw new ArgumentNullException(nameof(payloadReservation));
            if (log is null) throw new ArgumentNullException(nameof(log));

            await _initializationTask;
            var resToken = Guid.NewGuid().ToString("N");
            var document = new RentoomReservation
            {
                Id = resToken,
                ResToken = resToken,
                Reservation = payloadReservation
            };

            var payload = JsonConvert.SerializeObject(document);
            _dbContext.Reservations.Add(new ReservationEntity
            {
                ResToken = resToken,
                Payload = payload,
                CreatedAt = DateTime.UtcNow
            });

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
    }

}
