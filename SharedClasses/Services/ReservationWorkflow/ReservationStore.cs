using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationWorkflow;
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
        Task UpdateAsync(ReservationRecord record, CancellationToken cancellationToken = default);
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
            await _initializationTask;

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
            await _initializationTask;

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.ReservationRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservationGuid == reservationGuid, cancellationToken);

            return entity is null ? null : MapToRecord(entity);
        }

        public async Task UpdateAsync(ReservationRecord record, CancellationToken cancellationToken = default)
        {
            if (record is null) throw new ArgumentNullException(nameof(record));

         
            await using var context = _dbContextFactory.CreateDbContext();
            var entity = MapToEntity(record);
            //entity.RowVersion = Guid.NewGuid().ToByteArray();        
           // context.Entry(entity).Property(e => e.RowVersion).OriginalValue = record.RowVersion;
            //context.Entry(entity).Property(e => e.RowVersion).IsModified = true;
            //context.Entry(entity).State = EntityState.Modified;
            entity.UpdatedAt = DateTime.UtcNow;
            context.ReservationRecords.Update(entity);

            await context.SaveChangesAsync(cancellationToken);
            record.RowVersion = entity.RowVersion ?? Array.Empty<byte>();
        }


        private static ReservationRecord MapToRecord(ReservationRecordEntity entity)
        {
            var state = string.IsNullOrWhiteSpace(entity.ReservationJson)
                ? new ReservationState()
                : JsonConvert.DeserializeObject<ReservationState>(entity.ReservationJson) ?? new ReservationState();

            return new ReservationRecord
            {
                ReservationGuid = entity.ReservationGuid,
                State = state,
                IdoReservationId = entity.IdoReservationId,
                IdoStatus = entity.IdoStatus,
                PaymentSessionGuid = entity.PaymentSessionGuid,
                PaymentStatus = entity.PaymentStatus ?? PaymentStatuses.None,
                Provider = entity.Provider,
                ProviderTransactionId = entity.ProviderTransactionId,
                RowVersion = entity.RowVersion ?? Array.Empty<byte>(),
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt
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
                PaymentSessionGuid = record.PaymentSessionGuid,
                PaymentStatus = record.PaymentStatus,
                Provider = record.Provider,
                ProviderTransactionId = record.ProviderTransactionId,
                RowVersion = record.RowVersion,
                CreatedAt = record.CreatedAt,
                UpdatedAt = record.UpdatedAt
            };
        }
        private async Task EnsureCreatedAsync()
        {
            await using var context = _dbContextFactory.CreateDbContext();
            await context.Database.EnsureCreatedAsync();
        }
    }
}
