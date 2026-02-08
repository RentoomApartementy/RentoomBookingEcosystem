using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Linq;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellOrderStore
    {
        Task<UpsellOrderRecord> CreateAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default);
        Task<UpsellOrderRecord> CreateWithLinesAsync(UpsellOrderRequest request, IReadOnlyList<UpsellOrderLineRecord> lines, CancellationToken cancellationToken = default);
        Task<UpsellOrderRecord?> GetAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default);
        Task<List<UpsellOrderRecord>> GetByReservationGuidAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
        Task<List<UpsellOrderLineRecord>> GetLinesAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default);
        Task UpdateAsync(UpsellOrderRecord record, CancellationToken cancellationToken = default);
        Task ReplaceLinesAsync(Guid upsellOrderGuid, IReadOnlyList<UpsellOrderLineRecord> lines, CancellationToken cancellationToken = default);
        Task<UpsellOrderRecord?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken = default);
    }

    public class UpsellOrderStore : IUpsellOrderStore
    {
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly Task _initializationTask;

        public UpsellOrderStore(IDbContextFactory<PostgresBookingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _initializationTask = EnsureCreatedAsync();
        }

        public async Task<UpsellOrderRecord> CreateAsync(UpsellOrderRequest request, CancellationToken cancellationToken = default)
        {
            await _initializationTask;

            var orderGuid = Guid.NewGuid();
            var state = new UpsellOrderState
            {
                Request = request
            };

            var entity = new UpsellOrderRecordEntity
            {
                UpsellOrderGuid = orderGuid,
                ReservationGuid = request.ReservationGuid,
                UpsellOrderJson = JsonConvert.SerializeObject(state),
                PaymentStatus = Models.ReservationWorkflow.PaymentStatuses.None,
                Provider = "TPAY",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            await using var context = _dbContextFactory.CreateDbContext();
            context.UpsellOrderRecords.Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            return MapToRecord(entity);
        }

        public async Task<UpsellOrderRecord> CreateWithLinesAsync(UpsellOrderRequest request, IReadOnlyList<UpsellOrderLineRecord> lines, CancellationToken cancellationToken = default)
        {
            if (lines is null) throw new ArgumentNullException(nameof(lines));

            var record = await CreateAsync(request, cancellationToken);
            await ReplaceLinesAsync(record.UpsellOrderGuid, lines, cancellationToken);
            record.Lines = lines.ToList();
            return record;
        }

        public async Task<UpsellOrderRecord?> GetAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default)
        {
            await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.UpsellOrderRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.UpsellOrderGuid == upsellOrderGuid, cancellationToken);

            if (entity is null)
            {
                return null;
            }

            var record = MapToRecord(entity);
            record.Lines = await GetLinesAsync(record.UpsellOrderGuid, cancellationToken);
            return record;
        }

        public async Task<List<UpsellOrderRecord>> GetByReservationGuidAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entities = await context.UpsellOrderRecords.AsNoTracking()
                .Where(r => r.ReservationGuid == reservationGuid)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

            var records = entities.Select(MapToRecord).ToList();
            foreach (var record in records)
            {
                record.Lines = await GetLinesAsync(record.UpsellOrderGuid, cancellationToken);
            }

            return records;
        }

        public async Task<List<UpsellOrderLineRecord>> GetLinesAsync(Guid upsellOrderGuid, CancellationToken cancellationToken = default)
        {
            await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entities = await context.UpsellOrderLines.AsNoTracking()
                .Where(line => line.UpsellOrderGuid == upsellOrderGuid)
                .OrderBy(line => line.CreatedAt)
                .ToListAsync(cancellationToken);

            return entities.Select(MapLineToRecord).ToList();
        }

        public async Task UpdateAsync(UpsellOrderRecord record, CancellationToken cancellationToken = default)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

            await using var context = _dbContextFactory.CreateDbContext();
            var existing = await context.UpsellOrderRecords
                .FirstOrDefaultAsync(r => r.UpsellOrderGuid == record.UpsellOrderGuid, cancellationToken);

            if (existing is null)
            {
                return;
            }

            existing.UpsellOrderJson = JsonConvert.SerializeObject(record.State);
            existing.ReservationGuid = record.State.Request?.ReservationGuid;
            existing.PaymentSessionGuid = record.PaymentSessionGuid;
            existing.PaymentStatus = record.PaymentStatus;
            existing.Provider = record.Provider;
            existing.ProviderTransactionId = record.ProviderTransactionId;
            existing.PaidAtUtc = record.PaidAtUtc;
            existing.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task ReplaceLinesAsync(Guid upsellOrderGuid, IReadOnlyList<UpsellOrderLineRecord> lines, CancellationToken cancellationToken = default)
        {
            if (lines is null) throw new ArgumentNullException(nameof(lines));

            await using var context = _dbContextFactory.CreateDbContext();
            var existing = await context.UpsellOrderLines
                .Where(line => line.UpsellOrderGuid == upsellOrderGuid)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
            {
                context.UpsellOrderLines.RemoveRange(existing);
            }

            var entities = lines.Select(line => MapLineToEntity(upsellOrderGuid, line)).ToList();
            context.UpsellOrderLines.AddRange(entities);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<UpsellOrderRecord?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerTransactionId)) throw new ArgumentNullException(nameof(providerTransactionId));

            await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.UpsellOrderRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ProviderTransactionId == providerTransactionId, cancellationToken);

            return entity is null ? null : MapToRecord(entity);
        }

        private static UpsellOrderRecord MapToRecord(UpsellOrderRecordEntity entity)
        {
            var state = string.IsNullOrWhiteSpace(entity.UpsellOrderJson)
                ? new UpsellOrderState()
                : JsonConvert.DeserializeObject<UpsellOrderState>(entity.UpsellOrderJson) ?? new UpsellOrderState();

            return new UpsellOrderRecord
            {
                UpsellOrderGuid = entity.UpsellOrderGuid,
                State = state,
                Lines = new List<UpsellOrderLineRecord>(),
                PaymentSessionGuid = entity.PaymentSessionGuid,
                PaymentStatus = entity.PaymentStatus ?? Models.ReservationWorkflow.PaymentStatuses.None,
                Provider = entity.Provider,
                ProviderTransactionId = entity.ProviderTransactionId,
                PaidAtUtc = entity.PaidAtUtc,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static UpsellOrderLineRecord MapLineToRecord(UpsellOrderLineEntity entity)
        {
            return new UpsellOrderLineRecord
            {
                UpsellOrderLineGuid = entity.UpsellOrderLineGuid,
                UpsellOrderGuid = entity.UpsellOrderGuid,
                PartnerServiceId = entity.PartnerServiceId,
                TitleSnapshot = entity.TitleSnapshot,
                PricingModel = entity.PricingModel,
                Quantity = entity.Quantity,
                UnitPriceGross = entity.UnitPriceGross,
                Nights = entity.Nights,
                TotalGuests = entity.TotalGuests,
                LineTotalGross = entity.LineTotalGross,
                Currency = entity.Currency,
                LineStatus = entity.LineStatus,
                BitrixProductId = entity.BitrixProductId,
                BitrixLineId = entity.BitrixLineId,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
            };
        }

        private static UpsellOrderLineEntity MapLineToEntity(Guid upsellOrderGuid, UpsellOrderLineRecord line)
        {
            return new UpsellOrderLineEntity
            {
                UpsellOrderLineGuid = line.UpsellOrderLineGuid == Guid.Empty ? Guid.NewGuid() : line.UpsellOrderLineGuid,
                UpsellOrderGuid = upsellOrderGuid,
                PartnerServiceId = line.PartnerServiceId,
                TitleSnapshot = line.TitleSnapshot,
                PricingModel = line.PricingModel,
                Quantity = line.Quantity,
                UnitPriceGross = line.UnitPriceGross,
                Nights = line.Nights,
                TotalGuests = line.TotalGuests,
                LineTotalGross = line.LineTotalGross,
                Currency = line.Currency,
                LineStatus = line.LineStatus,
                BitrixProductId = line.BitrixProductId,
                BitrixLineId = line.BitrixLineId,
                CreatedAt = line.CreatedAt == default ? DateTime.UtcNow : line.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private async Task EnsureCreatedAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();
        }
    }
}
