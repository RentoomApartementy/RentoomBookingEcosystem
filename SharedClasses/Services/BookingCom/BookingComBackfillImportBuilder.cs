using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Services.BookingCom;

public interface IBookingComBackfillImportBuilder
{
    Task<BookingComBackfillPreparedImport> BuildAsync(int reservationId, CancellationToken cancellationToken = default);
}

public class BookingComBackfillImportBuilder : IBookingComBackfillImportBuilder
{
    private const string SourceProvidersSection = "BookingCom:BackfillSourceProviders";

    private readonly IdoSellService _idoSellService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BookingComBackfillImportBuilder> _logger;

    public BookingComBackfillImportBuilder(
        IdoSellService idoSellService,
        IConfiguration configuration,
        ILogger<BookingComBackfillImportBuilder> logger)
    {
        _idoSellService = idoSellService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BookingComBackfillPreparedImport> BuildAsync(int reservationId, CancellationToken cancellationToken = default)
    {
        var response = await _idoSellService.FetchReservationByIDFromIdoSellAsync(
            reservationId,
            false,
            cancellationToken: cancellationToken);

        var reservation = response.ReservationResponse?.result?.Reservations?.FirstOrDefault();
        if (reservation is null)
        {
            throw new InvalidOperationException($"IdoBooking reservation {reservationId} was not found while preparing backfill import.");
        }

        var details = reservation.ReservationDetails;
        var sourceTypeId = details?.reservationSourceTypeId ?? 0;
        var sourceId = details?.reservationSourceId?.Trim() ?? string.Empty;
        var provider = ResolveProvider(sourceTypeId, sourceId);
        var providerTransactionId = BuildProviderTransactionId(provider, sourceId);
        var subject = BookingComEmailMetadataHelper.BuildSyntheticSubject(reservation.id, provider, providerTransactionId);

        if (!string.Equals(provider, BookingComEmailMetadataHelper.DefaultProvider, StringComparison.OrdinalIgnoreCase) &&
            provider.StartsWith("IDB_SOURCE_", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "No configured provider mapping for reservationSourceTypeId {SourceTypeId}. Falling back to provider {Provider} for reservation {ReservationId}.",
                sourceTypeId,
                provider,
                reservation.id);
        }

        return new BookingComBackfillPreparedImport
        {
            IncomingEmail = new BookingComIncomingEmail
            {
                MessageId = $"backfill:{reservation.id}:{Guid.NewGuid():N}",
                ReceivedDateTime = DateTime.UtcNow.ToString("O"),
                Subject = subject,
                BodyHtml = JsonConvert.SerializeObject(new
                {
                    Kind = "backfill",
                    ReservationId = reservation.id,
                    SourceTypeId = sourceTypeId,
                    SourceId = sourceId,
                    details?.internalSourceId,
                    details?.status
                }),
                RawPayload = JsonConvert.SerializeObject(new
                {
                    Kind = "backfill",
                    ReservationId = reservation.id,
                    ReservationSourceTypeId = sourceTypeId,
                    ReservationSourceId = sourceId
                })
            },
            ProcessingContext = BookingComEmailMetadataHelper.CreateSyntheticContext(reservation.id, provider, providerTransactionId)
        };
    }

    private string ResolveProvider(int sourceTypeId, string sourceId)
    {
        var mappedProvider = _configuration[$"{SourceProvidersSection}:{sourceTypeId}"];
        if (!string.IsNullOrWhiteSpace(mappedProvider))
        {
            return mappedProvider.Trim();
        }

        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return BookingComEmailMetadataHelper.DefaultProvider;
        }

        return sourceTypeId > 0
            ? $"IDB_SOURCE_{sourceTypeId}"
            : BookingComEmailMetadataHelper.DefaultProvider;
    }

    private static string BuildProviderTransactionId(string provider, string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return BookingComEmailMetadataHelper.DefaultProviderTransactionId;
        }

        return $"{provider}: {sourceId}";
    }
}
