using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.BookingCom;
using System.Text.Json;

namespace RentoomBooking.SharedClasses.Services.BookingCom;

public interface IBookingComIncomingEmailProcessor
{
    Task<BookingComEmailProcessingResult> ProcessAsync(
        BookingComIncomingEmail email,
        BookingComEmailProcessingContext? context = null,
        CancellationToken cancellationToken = default);
}

public class BookingComIncomingEmailProcessor : IBookingComIncomingEmailProcessor
{
    private const string ProcessingEnabledKey = "BookingCom:ReservationProcessingEnabled";

    private readonly ILogger<BookingComIncomingEmailProcessor> _logger;
    private readonly IConfiguration _configuration;
    private readonly IBookingComLogStore _bookingComLogStore;
    private readonly IBookingComReservationWorkflowService _bookingComReservationWorkflowService;

    public BookingComIncomingEmailProcessor(
        ILogger<BookingComIncomingEmailProcessor> logger,
        IConfiguration configuration,
        IBookingComLogStore bookingComLogStore,
        IBookingComReservationWorkflowService bookingComReservationWorkflowService)
    {
        _logger = logger;
        _configuration = configuration;
        _bookingComLogStore = bookingComLogStore;
        _bookingComReservationWorkflowService = bookingComReservationWorkflowService;
    }

    public async Task<BookingComEmailProcessingResult> ProcessAsync(
        BookingComIncomingEmail email,
        BookingComEmailProcessingContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (email is null) throw new ArgumentNullException(nameof(email));

        var processingEnabled = _configuration.GetValue<bool>(ProcessingEnabledKey);
        var subject = email.Subject?.Trim() ?? string.Empty;
        var reservationId = context?.ReservationId ?? BookingComEmailMetadataHelper.ExtractReservationId(subject);
        var (provider, providerTransactionId) = ResolveProviderInfo(subject, context);
        var bodyLen = email.BodyHtml?.Length ?? 0;
        var bodySnippet = SafeSnippet(email.BodyHtml, 120);
        var source = string.IsNullOrWhiteSpace(context?.Source)
            ? BookingComEmailMetadataHelper.IncomingEmailSource
            : context!.Source;

        Guid? bookingComLogGuid = null;

        try
        {
            _logger.LogInformation(
                "Booking import request. Source={Source}; MessageId={MessageId}; ReceivedDateTime={ReceivedDateTime}; ReservationId={ReservationId}; Provider={Provider}; ProviderTransactionId={ProviderTransactionId}; Subject=\"{Subject}\"; BodyHtmlLen={BodyHtmlLen}; BodySnippet=\"{BodySnippet}\"",
                source,
                email.MessageId,
                email.ReceivedDateTime,
                reservationId,
                provider,
                providerTransactionId,
                subject,
                bodyLen,
                bodySnippet);

            bookingComLogGuid = await _bookingComLogStore.CreateAsync(email, reservationId, processingEnabled, cancellationToken);

            await AppendLogStepAsync(
                bookingComLogGuid.Value,
                "email_received",
                "Completed",
                context?.IsSynthetic == true
                    ? "Synthetic booking email was generated for reservation backfill."
                    : "Incoming External Partner email was received.",
                payload: new
                {
                    Source = source,
                    reservationId,
                    provider,
                    providerTransactionId,
                    Subject = subject,
                    BodyLength = bodyLen,
                    BodySnippet = bodySnippet
                },
                overallStatus: processingEnabled ? BookingComLogStatuses.Processing : BookingComLogStatuses.Disabled,
                cancellationToken: cancellationToken);

            if (!reservationId.HasValue)
            {
                await AppendLogStepAsync(
                    bookingComLogGuid.Value,
                    "reservation_id_missing",
                    "Ignored",
                    "No IdoBooking reservation id could be extracted from the email context.",
                    payload: new { Source = source, Subject = subject },
                    overallStatus: BookingComLogStatuses.Ignored,
                    cancellationToken: cancellationToken);

                return new BookingComEmailProcessingResult
                {
                    BookingComLogGuid = bookingComLogGuid,
                    Status = BookingComLogStatuses.Ignored,
                    Message = "Reservation id missing.",
                    MessageId = email.MessageId
                };
            }

            if (!processingEnabled)
            {
                await AppendLogStepAsync(
                    bookingComLogGuid.Value,
                    "processing_disabled",
                    "Skipped",
                    "External Partner reservation processing is switched off. Logged the incoming email only.",
                    payload: new { reservationId },
                    overallStatus: BookingComLogStatuses.Disabled,
                    cancellationToken: cancellationToken);

                return new BookingComEmailProcessingResult
                {
                    BookingComLogGuid = bookingComLogGuid,
                    ReservationId = reservationId,
                    Status = BookingComLogStatuses.Disabled,
                    Message = "Processing disabled.",
                    MessageId = email.MessageId
                };
            }

            var result = await _bookingComReservationWorkflowService.ProcessIncomingReservationAsync(
                new BookingComReservationImportRequest
                {
                    BookingComLogGuid = bookingComLogGuid.Value,
                    ReservationId = reservationId.Value,
                    IncomingEmail = email,
                    Provider = provider,
                    ProviderTransactionId = providerTransactionId
                },
                cancellationToken);

            return new BookingComEmailProcessingResult
            {
                BookingComLogGuid = result.BookingComLogGuid,
                ReservationId = result.ReservationId,
                ReservationGuid = result.ReservationGuid,
                EmailConfirmed = result.EmailConfirmed,
                Status = result.Status,
                Message = result.Message,
                MessageId = email.MessageId,
                Provider = result.Provider ?? provider,
                ProviderTransactionId = result.ProviderTransactionId ?? providerTransactionId
            };
        }
        catch (Exception ex)
        {
            if (bookingComLogGuid.HasValue)
            {
                try
                {
                    await AppendLogStepAsync(
                        bookingComLogGuid.Value,
                        "processing_failed",
                        "Failed",
                        "External Partner email processing failed.",
                        payload: new
                        {
                            Source = source,
                            Exception = ex.Message
                        },
                        overallStatus: BookingComLogStatuses.Failed,
                        cancellationToken: cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "Failed to append External Partner failure log step for {LogGuid}.", bookingComLogGuid.Value);
                }
            }

            _logger.LogError(ex, "External Partner email processing failed for source {Source}.", source);

            return new BookingComEmailProcessingResult
            {
                BookingComLogGuid = bookingComLogGuid,
                ReservationId = reservationId,
                Status = BookingComLogStatuses.Failed,
                Message = ex.Message,
                MessageId = email.MessageId
            };
        }
    }

    private async Task AppendLogStepAsync(
        Guid bookingComLogGuid,
        string step,
        string status,
        string message,
        object? payload = null,
        string? overallStatus = null,
        CancellationToken cancellationToken = default)
    {
        await _bookingComLogStore.AppendStepAsync(
            bookingComLogGuid,
            new BookingComLogStep
            {
                OccurredAtUtc = DateTime.UtcNow,
                Step = step,
                Status = status,
                Message = message,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload)
            },
            overallStatus,
            cancellationToken: cancellationToken);
    }

    private static (string Provider, string ProviderTransactionId) ResolveProviderInfo(
        string subject,
        BookingComEmailProcessingContext? context)
    {
        if (!string.IsNullOrWhiteSpace(context?.Provider) ||
            !string.IsNullOrWhiteSpace(context?.ProviderTransactionId))
        {
            var provider = string.IsNullOrWhiteSpace(context?.Provider)
                ? BookingComEmailMetadataHelper.DefaultProvider
                : context.Provider.Trim();

            var providerTransactionId = string.IsNullOrWhiteSpace(context?.ProviderTransactionId)
                ? BookingComEmailMetadataHelper.DefaultProviderTransactionId
                : context.ProviderTransactionId.Trim();

            return (provider, providerTransactionId);
        }

        return BookingComEmailMetadataHelper.ExtractProviderInfo(subject);
    }

    private static string SafeSnippet(string? html, int maxLen)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var compact = html.Replace("\r", " ").Replace("\n", " ").Trim();
        if (compact.Length <= maxLen)
        {
            return compact;
        }

        return compact[..maxLen] + "...";
    }
}
