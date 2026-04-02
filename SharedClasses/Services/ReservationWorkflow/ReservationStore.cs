using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow
{
    public interface IReservationStore
    {
        Task<ReservationRecord> CreateAsync(StartReservationRequest request, CancellationToken cancellationToken = default);
        Task<ReservationRecord?> GetAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
        Task<ReservationRecord?> GetByIdoReservationIdAsync(int idoReservationId, CancellationToken cancellationToken = default);
        Task UpdateAsync(ReservationRecord record, CancellationToken cancellationToken = default);
        Task<ReservationRecord?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ReservationRecord>> ListActiveWithIdoReservationAsync(CancellationToken cancellationToken = default);
    }

    public class ReservationStore : IReservationStore
    {
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly Task _initializationTask;

        public ReservationStore(IDbContextFactory<PostgresBookingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _initializationTask = EnsureCreatedAsync();
        }

        public async Task<ReservationRecord> CreateAsync(StartReservationRequest request, CancellationToken cancellationToken = default)
        {
            // await _initializationTask;

            var reservationGuid = Guid.NewGuid();
            var state = new ReservationState
            {
                StartRequest = request
            };

            var entity = new ReservationRecordEntity
            {
                ReservationGuid = reservationGuid,
                ReservationJson = JsonConvert.SerializeObject(state),
                PaymentStatus = PaymentStatuses.None,
                Provider = "TPAY",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RowVersion = Guid.NewGuid().ToByteArray()
            };

            await using var context = _dbContextFactory.CreateDbContext();
            context.ReservationRecords.Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            return MapToRecord(entity);
        }

        public async Task<ReservationRecord?> GetAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            // await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.ReservationRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationGuid == reservationGuid, cancellationToken);

            return entity is null ? null : MapToRecord(entity);
        }

        public async Task<ReservationRecord?> GetByIdoReservationIdAsync(int idoReservationId, CancellationToken cancellationToken = default)
        {
            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.ReservationRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdoReservationId == idoReservationId, cancellationToken);

            return entity is null ? null : MapToRecord(entity);
        }

        public async Task UpdateAsync(ReservationRecord record, CancellationToken cancellationToken = default)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

            if (record.State.StartRequest?.SelectedAddons?.Count > 0 && record.State.StartRequest.SelectedAddons[0].PaymentType == null)
            {
                               Console.WriteLine($"[ReservationStore] Warning: Attempting to update ReservationRecord with ReservationGuid {record.ReservationGuid} where the first selected addon's PaymentType is null. This may indicate an issue with the data being saved.");
            }

            await using var context = _dbContextFactory.CreateDbContext();
            var existing = await context.ReservationRecords
                .FirstOrDefaultAsync(r => r.ReservationGuid == record.ReservationGuid, cancellationToken);

            if (existing is null)
            {
                return;
            }

            existing.ReservationJson = JsonConvert.SerializeObject(record.State);
            existing.IdoReservationId = record.IdoReservationId;
            existing.IdoStatus = record.IdoStatus;
            existing.ClientBitrixId = record.ClientBitrixId;
            existing.DealBitrixId = record.DealBitrixId;
            existing.PaymentSessionGuid = record.PaymentSessionGuid;
            existing.PaymentStatus = record.PaymentStatus;
            existing.Provider = record.Provider;
            existing.ProviderTransactionId = record.ProviderTransactionId;
            existing.SyncChangeSummary = record.SyncChangeSummary;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.ConfirmationEmailBitrixId = record.DealBitrixSentConfirmationEmailId;
            await context.SaveChangesAsync(cancellationToken);

        }


        private static ReservationRecord MapToRecord(ReservationRecordEntity entity)
        {
            if (!entity.ReservationJson.ToLower().Contains("paymenttype"))
            {
                Console.WriteLine($"[ReservationStore] Warning: ReservationJson for ReservationGuid {entity.ReservationGuid} does not contain PaymentType. This may indicate an older record that needs to be updated.");
            }

            var state = string.IsNullOrWhiteSpace(entity.ReservationJson)
                ? new ReservationState()
                : JsonConvert.DeserializeObject<ReservationState>(entity.ReservationJson) ?? new ReservationState();

            return new ReservationRecord
            {
                ReservationGuid = entity.ReservationGuid,
                State = state,
                IdoReservationId = entity.IdoReservationId,
                IdoStatus = entity.IdoStatus,
                ClientBitrixId = entity.ClientBitrixId,
                DealBitrixId = entity.DealBitrixId,
                PaymentSessionGuid = entity.PaymentSessionGuid,
                PaymentStatus = entity.PaymentStatus ?? PaymentStatuses.None,
                Provider = entity.Provider,
                ProviderTransactionId = entity.ProviderTransactionId,
                SyncChangeSummary = entity.SyncChangeSummary,
                RowVersion = entity.RowVersion ?? Array.Empty<byte>(),
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                DealBitrixSentConfirmationEmailId = entity.ConfirmationEmailBitrixId
            };
        }

        private static ReservationRecordEntity MapToEntity(ReservationRecord record)
        {
            return new ReservationRecordEntity
            {
                ReservationGuid = record.ReservationGuid,
                ReservationJson = JsonConvert.SerializeObject(record.State),
                IdoReservationId = record.IdoReservationId,
                IdoStatus = record.IdoStatus,
                ClientBitrixId = record.ClientBitrixId,
                DealBitrixId = record.DealBitrixId,
                PaymentSessionGuid = record.PaymentSessionGuid,
                PaymentStatus = record.PaymentStatus,
                Provider = record.Provider,
                ProviderTransactionId = record.ProviderTransactionId,
                SyncChangeSummary = record.SyncChangeSummary,
                RowVersion = record.RowVersion,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt,
                ConfirmationEmailBitrixId = record.DealBitrixSentConfirmationEmailId
            };
        }
        private async Task EnsureCreatedAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();
        }

        public async Task<ReservationRecord?> GetByProviderTransactionIdAsync(string providerTransactionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerTransactionId)) throw new ArgumentNullException(nameof(providerTransactionId));

            // await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.ReservationRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ProviderTransactionId == providerTransactionId, cancellationToken);

            return entity is null ? null : MapToRecord(entity);
        }

        public async Task<IReadOnlyList<ReservationRecord>> ListActiveWithIdoReservationAsync(CancellationToken cancellationToken = default)
        {
            await using var context = _dbContextFactory.CreateDbContext();

            var entities = await context.ReservationRecords.AsNoTracking()
                .Where(r => r.IdoReservationId.HasValue)
                .Where(r => r.IdoStatus == null
                    || (r.IdoStatus != ReservationStatusType.Canceled
                        && r.IdoStatus != ReservationStatusType.Completed))
                .OrderBy(r => r.UpdatedAt)
                .ToListAsync(cancellationToken);

            return entities.Select(MapToRecord).ToList();
        }
    }
}
