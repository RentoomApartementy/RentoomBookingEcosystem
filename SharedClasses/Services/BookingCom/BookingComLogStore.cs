using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;

namespace RentoomBooking.SharedClasses.Services.BookingCom
{
    public interface IBookingComLogStore
    {
        Task<Guid> CreateAsync(BookingComIncomingEmail incomingEmail, int? reservationId, bool processingEnabled, CancellationToken cancellationToken = default);
        Task AppendStepAsync(Guid bookingComLogGuid, BookingComLogStep step, string? overallStatus = null, Guid? reservationGuid = null, CancellationToken cancellationToken = default);
    }

    public class BookingComLogStore : IBookingComLogStore
    {
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;

        public BookingComLogStore(IDbContextFactory<PostgresBookingDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<Guid> CreateAsync(BookingComIncomingEmail incomingEmail, int? reservationId, bool processingEnabled, CancellationToken cancellationToken = default)
        {
            if (incomingEmail is null) throw new ArgumentNullException(nameof(incomingEmail));

            var entity = new BookingComLogEntity
            {
                BookingComLogGuid = Guid.NewGuid(),
                ReservationId = reservationId,
                MessageId = incomingEmail.MessageId,
                Subject = incomingEmail.Subject,
                ProcessingEnabled = processingEnabled,
                Status = processingEnabled ? BookingComLogStatuses.Pending : BookingComLogStatuses.Disabled,
                IncomingEmailJson = JsonConvert.SerializeObject(incomingEmail),
                StepsJson = JsonConvert.SerializeObject(new List<BookingComLogStep>()),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await using var context = _dbContextFactory.CreateDbContext();
            context.BookingComLogs.Add(entity);
            await context.SaveChangesAsync(cancellationToken);

            return entity.BookingComLogGuid;
        }

        public async Task AppendStepAsync(Guid bookingComLogGuid, BookingComLogStep step, string? overallStatus = null, Guid? reservationGuid = null, CancellationToken cancellationToken = default)
        {
            if (step is null) throw new ArgumentNullException(nameof(step));

            await using var context = _dbContextFactory.CreateDbContext();
            var entity = await context.BookingComLogs.FirstOrDefaultAsync(log => log.BookingComLogGuid == bookingComLogGuid, cancellationToken);
            if (entity is null)
            {
                throw new InvalidOperationException($"Booking.com log {bookingComLogGuid} not found.");
            }

            var steps = string.IsNullOrWhiteSpace(entity.StepsJson)
                ? new List<BookingComLogStep>()
                : JsonConvert.DeserializeObject<List<BookingComLogStep>>(entity.StepsJson) ?? new List<BookingComLogStep>();

            steps.Add(step);
            entity.StepsJson = JsonConvert.SerializeObject(steps);
            entity.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(overallStatus))
            {
                entity.Status = overallStatus;
            }

            if (reservationGuid.HasValue)
            {
                entity.ReservationGuid = reservationGuid.Value;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
