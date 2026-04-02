using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.BookingCom;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;

namespace RentoomBooking.SharedClasses.Services.BookingCom
{
    public interface IBookingComReservationWorkflowService
    {
        Task<BookingComReservationImportResult> ProcessIncomingReservationAsync(BookingComReservationImportRequest request, CancellationToken cancellationToken = default);
        Task<Guid?> CheckForDuplicate(int idoBookingId);
    }

    public class BookingComReservationWorkflowService : IBookingComReservationWorkflowService
    {
        private const string DefaultProvider = "IDB_PANEL";
        private const string DefaultProviderTransactionId = "IDB_PANEL_TRANSACTION";
        private const string DevelopmentPhoneOverride = "+48602394436";
        private const int BitrixEmailPollAttempts = 3;
        private static readonly TimeSpan BitrixEmailPollDelay = TimeSpan.FromSeconds(2);

        private readonly IdoSellService _idoApi;
        private readonly IReservationWorkflowService _reservationWorkflowService;
        private readonly IReservationSyncService _reservationSyncService;
        private readonly IReservationStore _reservationStore;
        private readonly ApartmentRepository _apartmentRepository;
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly IBookingComLogStore _logStore;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BookingComReservationWorkflowService> _logger;

        public BookingComReservationWorkflowService(
            IdoSellService idoApi,
            IReservationWorkflowService reservationWorkflowService,
            IReservationSyncService reservationSyncService,
            IReservationStore reservationStore,
            ApartmentRepository apartmentRepository,
            PostgresBookingDatabase bookingDatabase,
            IBookingComLogStore logStore,
            IConfiguration configuration,
            ILogger<BookingComReservationWorkflowService> logger)
        {
            _idoApi = idoApi ?? throw new ArgumentNullException(nameof(idoApi));
            _reservationWorkflowService = reservationWorkflowService ?? throw new ArgumentNullException(nameof(reservationWorkflowService));
            _reservationSyncService = reservationSyncService ?? throw new ArgumentNullException(nameof(reservationSyncService));
            _reservationStore = reservationStore ?? throw new ArgumentNullException(nameof(reservationStore));
            _apartmentRepository = apartmentRepository ?? throw new ArgumentNullException(nameof(apartmentRepository));
            _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
            _logStore = logStore ?? throw new ArgumentNullException(nameof(logStore));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BookingComReservationImportResult> ProcessIncomingReservationAsync(BookingComReservationImportRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var result = new BookingComReservationImportResult
            {
                BookingComLogGuid = request.BookingComLogGuid,
                ReservationId = request.ReservationId,
                Status = BookingComLogStatuses.Processing
            };
            var provider = string.IsNullOrWhiteSpace(request.Provider) ? DefaultProvider : request.Provider.Trim();
            var providerTransactionId = string.IsNullOrWhiteSpace(request.ProviderTransactionId)
                ? DefaultProviderTransactionId
                : request.ProviderTransactionId.Trim();

            try
            {
                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "processing_started",
                    "Started",
                    $"Started External Partner reservation import for IdoBooking reservation {request.ReservationId}.",
                    payload: new
                    {
                        request.IncomingEmail.MessageId,
                        request.IncomingEmail.Subject,
                        Provider = provider,
                        ProviderTransactionId = providerTransactionId
                    },
                    overallStatus: BookingComLogStatuses.Processing,
                    cancellationToken: cancellationToken);

                var reservationResponse = await _idoApi.FetchReservationByIDFromIdoSellAsync(
                    request.ReservationId,
                    false,
                    cancellationToken: cancellationToken);

                var reservation = reservationResponse.ReservationResponse?.result?.Reservations?.FirstOrDefault();
                if (reservation is null)
                {
                    throw new InvalidOperationException($"IdoBooking reservation {request.ReservationId} was not found.");
                }

               /*if (reservation?.ReservationDetails?.status !=ReservationStatusType.Accepted)
                {
                    throw new InvalidOperationException($"IdoBooking reservation {request.ReservationId} has a not acceptable status {reservation?.ReservationDetails?.status}.");
                }*/


                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "reservation_fetched",
                    "Completed",
                    $"Fetched reservation {reservation.id} from IdoBooking.",
                    payload: new
                    {
                        reservation.id,
                        Status = reservation.ReservationDetails?.status,
                        Items = reservation.Items?.Count ?? 0,
                        reservation.ReservationDetails?.currency
                    },
                    cancellationToken: cancellationToken);

                var paymentsResponse = await _idoApi.GetPaymentsAsync(
                    new PaymentGetParams
                    {
                        ReservationIds = new List<int> { request.ReservationId }
                    },
                    cancellationToken: cancellationToken);

                var payments = paymentsResponse?.Results?
                    .Where(payment => payment.ReservationId == request.ReservationId)
                    .OrderBy(payment => payment.Id)
                    .ToList()
                    ?? new List<PaymentDetails>();

                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "payments_fetched",
                    "Completed",
                    $"Fetched {payments.Count} payment entries from IdoBooking.",
                    payload: payments.Select(payment => new
                    {
                        payment.Id,
                        payment.Status,
                        payment.Type,
                        payment.Value,
                        payment.Currency,
                        payment.PaymentSystem,
                        payment.ExternalPaymentId
                    }).ToList(),
                    cancellationToken: cancellationToken);

                var startRequest = await MapStartRequestAsync(reservation, request.BookingComLogGuid, cancellationToken);
                var clientInfo = MapClientInfo(reservation.Client);
                InvoiceInfoDto? invoiceInfo = null;

                if (IsDevelopmentEnvironment())
                {
                    clientInfo.Phone = DevelopmentPhoneOverride;
                    clientInfo.Email = $"posith+booking_com_{Guid.NewGuid():N}@gmail.com";

                    await LogInfoAsync(
                        request.BookingComLogGuid,
                        "development_safeguards_applied",
                        "Completed",
                        "Applied development-only client overrides before Bitrix synchronization.",
                        payload: new
                        {
                            clientInfo.Phone,
                            clientInfo.Email
                        },
                        cancellationToken: cancellationToken);
                }

                var existingRecord = await _reservationStore.GetByIdoReservationIdAsync(request.ReservationId, cancellationToken);
                var isDuplicate = existingRecord is not null;
                Guid reservationGuid;

                if (existingRecord is null)
                {
                    reservationGuid = await _reservationWorkflowService.StartAsync(startRequest);
                    await LogInfoAsync(
                        request.BookingComLogGuid,
                        "workflow_record_created",
                        "Completed",
                        $"Created reservation workflow record {reservationGuid} for External Partner import.",
                        reservationGuid: reservationGuid,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    reservationGuid = existingRecord.ReservationGuid;
                    await _reservationWorkflowService.UpdateStartRequestAsync(reservationGuid, startRequest);
                    await LogInfoAsync(
                        request.BookingComLogGuid,
                        "workflow_record_reused",
                        "Completed",
                        $"Reused existing reservation workflow record {reservationGuid}.",
                        reservationGuid: reservationGuid,
                        cancellationToken: cancellationToken);

                    await LogWarningAsync(
                        request.BookingComLogGuid,
                        "duplicate_reservation_detected",
                        $"Reservation {request.ReservationId} already exists under workflow record {reservationGuid}. Synchronizing statuses and Bitrix fields.",
                        payload: new
                        {
                            request.ReservationId,
                            reservationGuid
                        },
                        overallStatus: BookingComLogStatuses.Duplicate,
                        reservationGuid: reservationGuid,
                        cancellationToken: cancellationToken);
                }

                result.ReservationGuid = reservationGuid;

                await _reservationWorkflowService.SaveClientInfoAsync(reservationGuid, clientInfo, invoiceInfo);
                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "client_saved",
                    "Completed",
                    $"Saved client data to reservation workflow record {reservationGuid}.",
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                reservation = await EnsureStayWellLinkInExternalNoteAsync(
                    reservation,
                    reservationGuid,
                    request.BookingComLogGuid,
                    cancellationToken);

                var storedToken = await _bookingDatabase.SaveReservationJsonAsync(
                    reservation,
                    _logger,
                    reservationGuid.ToString("D"),
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(storedToken))
                {
                    throw new InvalidOperationException($"Failed to store reservation {reservation.id} in backend database.");
                }

              


                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "reservation_saved",
                    "Completed",
                    $"Saved raw reservation payload to backend with token {storedToken}.",
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                var record = await _reservationStore.GetAsync(reservationGuid, cancellationToken)
                    ?? throw new InvalidOperationException($"Reservation workflow record {reservationGuid} was not found.");

                record.IdoReservationId = reservation.id;
                record.IdoStatus = reservation.ReservationDetails?.status;
                record.Provider = provider;
                record.ProviderTransactionId = providerTransactionId;
                record.PaymentStatus = MapWorkflowPaymentStatus(payments);

                await _reservationStore.UpdateAsync(record, cancellationToken);

                var processedAmount = payments
                    .Where(payment => string.Equals(payment.Status, PaymentStatus.Processed, StringComparison.OrdinalIgnoreCase))
                    .Sum(payment => Convert.ToDecimal(payment.Value, CultureInfo.InvariantCulture));

                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "workflow_record_bound",
                    "Completed",
                    $"Bound IdoBooking reservation {reservation.id} and payment state to workflow record.",
                    payload: new
                    {
                        record.Provider,
                        record.PaymentStatus,
                        record.ProviderTransactionId,
                        ProcessedAmount = processedAmount
                    },
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                await _reservationSyncService.FinalizeImportedReservationAsync(
                    reservationGuid,
                    new ImportedReservationFinalizationRequest
                    {
                        Provider = provider,
                        ProviderTransactionId = record.ProviderTransactionId ?? providerTransactionId,
                        PaymentStatus = record.PaymentStatus,
                        IdoStatus = record.IdoStatus,
                        UpdateReason = isDuplicate
                            ? "External Partner duplicate reservation synchronized"
                            : "External Partner reservation imported"
                    });

                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "workflow_finalized",
                    "Completed",
                    "Synchronized Bitrix artifacts for imported reservation.",
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                var emailConfirmed = false;
                if (string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
                {
                    emailConfirmed = await WaitForBitrixEmailConfirmationAsync(
                        request.BookingComLogGuid,
                        reservationGuid,
                        cancellationToken);
                }
                else
                {
                    await LogInfoAsync(
                        request.BookingComLogGuid,
                        "bitrix_email_poll_skipped",
                        "Skipped",
                        "Skipped Bitrix email confirmation polling because no processed IdoBooking payment was found.",
                        payload: new
                        {
                            record.PaymentStatus,
                            ProcessedAmount = processedAmount
                        },
                        reservationGuid: reservationGuid,
                        cancellationToken: cancellationToken);
                }

                var finalMessage = emailConfirmed
                    ? isDuplicate
                        ? "Duplicate External Partner reservation synchronized and Bitrix email was confirmed."
                        : "External Partner reservation import completed and Bitrix email was confirmed."
                    : string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                        ? isDuplicate
                            ? "Duplicate External Partner reservation synchronized, but Bitrix email was not confirmed within the polling window."
                            : "External Partner reservation import completed, but Bitrix email was not confirmed within the polling window."
                        : isDuplicate
                            ? "Duplicate External Partner reservation synchronized."
                            : "External Partner reservation import completed.";

                await LogInfoAsync(
                    request.BookingComLogGuid,
                    "processing_completed",
                    "Completed",
                    finalMessage,
                    payload: new
                    {
                        record.PaymentStatus,
                        EmailConfirmed = emailConfirmed
                    },
                    overallStatus: isDuplicate ? BookingComLogStatuses.Duplicate : BookingComLogStatuses.Completed,
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                result.ReservationGuid = reservationGuid;
                result.EmailConfirmed = emailConfirmed;
                result.Status = isDuplicate ? BookingComLogStatuses.Duplicate : BookingComLogStatuses.Completed;
                result.Message = finalMessage;
                return result;
            }
            catch (Exception ex)
            {
                await LogErrorAsync(
                    request.BookingComLogGuid,
                    "processing_failed",
                    $"External Partner reservation import failed for IdoBooking reservation {request.ReservationId}.",
                    ex,
                    overallStatus: BookingComLogStatuses.Failed,
                    reservationGuid: result.ReservationGuid,
                    cancellationToken: cancellationToken);

                throw;
            }
        }

        private async Task<StartReservationRequest> MapStartRequestAsync(Reservation reservation, Guid bookingComLogGuid, CancellationToken cancellationToken)
        {
            var items = reservation.Items ?? new List<ReservationItem>();
            if (items.Count == 0)
            {
                throw new InvalidOperationException($"Reservation {reservation.id} does not contain any items.");
            }

            if (items.Count > 1)
            {
                await LogWarningAsync(
                    bookingComLogGuid,
                    "reservation_multiple_items",
                    $"Reservation {reservation.id} contains {items.Count} items. The External Partner import maps the first item into the single-item workflow model and sums guest and price totals.",
                    payload: new
                    {
                        reservation.id,
                        Items = items.Count
                    },
                    cancellationToken: cancellationToken);
            }

            var details = reservation.ReservationDetails ?? throw new InvalidOperationException($"Reservation {reservation.id} is missing details.");
            var dateFrom = details.getDateFrom();
            var dateTo = details.getDateTo();
            var startDate = DateOnly.FromDateTime(dateFrom);
            var endDate = DateOnly.FromDateTime(dateTo);
            var totalNights = Math.Max(1, endDate.DayNumber - startDate.DayNumber);
            var primaryItem = items[0];
            var definedAddons = await _apartmentRepository.GetDefinedAddonsAsync(cancellationToken);
            var definedAddonsLookup = definedAddons.ToDictionary(addon => addon.IdoBookingId);

            var selectedAddons = new List<SelectedAddonDto>();
            foreach (var addon in items.SelectMany(item => item.addons ?? new List<ReservationAddon>()))
            {
                if (!int.TryParse(addon.addonId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var addonId))
                {
                    continue;
                }

                var fallbackPaymentType = InferFallbackAddonPaymentType(addon);
                var paymentType = definedAddonsLookup.TryGetValue(addonId, out var definedAddon)
                    ? definedAddon.PaymentType
                    : fallbackPaymentType;

                if (!definedAddonsLookup.ContainsKey(addonId))
                {
                    await LogWarningAsync(
                        bookingComLogGuid,
                        "addon_definition_missing",
                        $"Addon {addonId} is not defined in backend configuration. Applied fallback pricing model {paymentType}.",
                        payload: new
                        {
                            reservation.id,
                            addonId,
                            addon.addonName
                        },
                        cancellationToken: cancellationToken);
                }

                selectedAddons.Add(new SelectedAddonDto
                {
                    AddonId = addonId,
                    Persons = addon.persons ?? 1,
                    Nights = addon.nights ?? totalNights,
                    Quantity = addon.quantity ?? 1,
                    Price = ParseSingle(addon.price),
                    Vat = addon.vat,
                    PaymentType = paymentType,
                    DisplayText = !string.IsNullOrWhiteSpace(addon.addonName)
                        ? addon.addonName
                        : definedAddon?.Name ?? string.Empty
                });
            }

            return new StartReservationRequest
            {
                ObjectId = primaryItem.objectId,
                ObjectItemId = primaryItem.objectItemId,
                StartDate = startDate,
                EndDate = endDate,
                CheckInTime = new TimeOnly(15, 0), //TimeOnly.FromDateTime(dateFrom),
                CheckOutTime = new TimeOnly(11,0),//TimeOnly.FromDateTime(dateTo),
                Adults = items.Sum(item => item.numberOfAdults ?? 0),
                Children = items.Sum(item => ParseInt(item.numberOfSmallChildren)),
                OfferPrice = Convert.ToDecimal(items.Sum(item => item.price), CultureInfo.InvariantCulture),
                Currency = string.IsNullOrWhiteSpace(details.currency) ? "PLN" : details.currency,
                SelectedAddons = selectedAddons
            };
        }

        private static ClientInfoDto MapClientInfo(ClientModel? client)
        {
            if (client is null)
            {
                return new ClientInfoDto();
            }

            return new ClientInfoDto
            {
                FirstName = client.FirstName ?? string.Empty,
                LastName = client.LastName ?? string.Empty,
                Email = client.Email ?? string.Empty,
                Phone = client.Phone ?? string.Empty,
                Street = client.Street ?? string.Empty,
                City = client.City ?? string.Empty,
                ZipCode = client.Zipcode ?? string.Empty,
                CountryCode = string.IsNullOrWhiteSpace(client.CountryCode) ? "pl" : client.CountryCode,
                Language = string.IsNullOrWhiteSpace(client.Language) ? "pol" : client.Language
            };
        }

        private async Task<bool> WaitForBitrixEmailConfirmationAsync(Guid bookingComLogGuid, Guid reservationGuid, CancellationToken cancellationToken)
        {
            await LogInfoAsync(
                bookingComLogGuid,
                "bitrix_email_poll_started",
                "Started",
                $"Started polling Bitrix email confirmation for reservation {reservationGuid}.",
                reservationGuid: reservationGuid,
                cancellationToken: cancellationToken);

            for (var attempt = 1; attempt <= BitrixEmailPollAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var emailStatus = await _reservationWorkflowService.GetDealEmailStatusAsync(reservationGuid);
                if (emailStatus.EmailSent)
                {
                    await LogInfoAsync(
                        bookingComLogGuid,
                        "bitrix_email_confirmed",
                        "Completed",
                        $"Bitrix confirmation email was confirmed on attempt {attempt}.",
                        payload: new
                        {
                            Attempt = attempt,
                            emailStatus.HasActivities,
                            LatestActivityId = emailStatus.LatestActivity?.Id
                        },
                        reservationGuid: reservationGuid,
                        cancellationToken: cancellationToken);
                    return true;
                }

                await LogInfoAsync(
                    bookingComLogGuid,
                    "bitrix_email_poll_attempt",
                    "Pending",
                    $"Bitrix confirmation email not confirmed yet. Poll attempt {attempt}/{BitrixEmailPollAttempts}.",
                    payload: new
                    {
                        Attempt = attempt,
                        emailStatus.HasActivities,
                        LatestActivityId = emailStatus.LatestActivity?.Id
                    },
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                if (attempt < BitrixEmailPollAttempts)
                {
                    await Task.Delay(BitrixEmailPollDelay, cancellationToken);
                }
            }

            await LogWarningAsync(
                bookingComLogGuid,
                "bitrix_email_poll_timeout",
                $"Bitrix confirmation email was not confirmed after {BitrixEmailPollAttempts} polling attempts.",
                reservationGuid: reservationGuid,
                cancellationToken: cancellationToken);

            return false;
        }

        public async Task<Guid?> CheckForDuplicate(int idoBookingId)
        {
            var existing = await _reservationStore.GetByIdoReservationIdAsync(idoBookingId);


            if (existing != null)
            {
                _logger.LogWarning("Duplicate reservation detected for IdoBooking reservation {Id}. Existing reservation GUID: {ExistingGuid}", idoBookingId, existing.ReservationGuid);
                return existing?.ReservationGuid;
            }
        return null;

        }

        private async Task LogInfoAsync(
            Guid bookingComLogGuid,
            string step,
            string status,
            string message,
            object? payload = null,
            string? overallStatus = null,
            Guid? reservationGuid = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("External Partner import {LogGuid} [{Step}] {Message}", bookingComLogGuid, step, message);

            await _logStore.AppendStepAsync(
                bookingComLogGuid,
                new BookingComLogStep
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    Step = step,
                    Status = status,
                    Message = message,
                    PayloadJson = SerializePayload(payload)
                },
                overallStatus,
                reservationGuid,
                cancellationToken);
        }

        private async Task LogWarningAsync(
            Guid bookingComLogGuid,
            string step,
            string message,
            object? payload = null,
            string? overallStatus = null,
            Guid? reservationGuid = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("External Partner import {LogGuid} [{Step}] {Message}", bookingComLogGuid, step, message);

            await _logStore.AppendStepAsync(
                bookingComLogGuid,
                new BookingComLogStep
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    Step = step,
                    Status = "Warning",
                    Message = message,
                    PayloadJson = SerializePayload(payload)
                },
                overallStatus,
                reservationGuid,
                cancellationToken);
        }

        private async Task LogErrorAsync(
            Guid bookingComLogGuid,
            string step,
            string message,
            Exception exception,
            object? payload = null,
            string? overallStatus = null,
            Guid? reservationGuid = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogError(exception, "External Partner import {LogGuid} [{Step}] {Message}", bookingComLogGuid, step, message);

            await _logStore.AppendStepAsync(
                bookingComLogGuid,
                new BookingComLogStep
                {
                    OccurredAtUtc = DateTime.UtcNow,
                    Step = step,
                    Status = "Failed",
                    Message = message,
                    PayloadJson = SerializePayload(new
                    {
                        Exception = exception.Message,
                        Payload = payload
                    })
                },
                overallStatus,
                reservationGuid,
                cancellationToken);
        }

        private static string MapWorkflowPaymentStatus(IEnumerable<PaymentDetails> payments)
        {
            var paymentList = payments?.ToList() ?? new List<PaymentDetails>();

            if (paymentList.Any(payment => string.Equals(payment.Status, PaymentStatus.Processed, StringComparison.OrdinalIgnoreCase)))
            {
                return PaymentStatuses.Paid;
            }

            if (paymentList.Any(payment => string.Equals(payment.Status, PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase)))
            {
                return PaymentStatuses.Initiated;
            }

            if (paymentList.Any(payment => string.Equals(payment.Status, PaymentStatus.Cancelled, StringComparison.OrdinalIgnoreCase)))
            {
                return PaymentStatuses.Failed;
            }

            return PaymentStatuses.None;
        }

        private static AddonPaymentType InferFallbackAddonPaymentType(ReservationAddon addon)
        {
            if (addon.persons.HasValue && addon.nights.HasValue)
            {
                return AddonPaymentType.PayPerPersonPerNight;
            }

            if (addon.nights.HasValue)
            {
                return AddonPaymentType.PayPerNight;
            }

            if (addon.quantity.GetValueOrDefault() > 1)
            {
                return AddonPaymentType.PayPerAmount;
            }

            return AddonPaymentType.PayPerStay;
        }

        private static int ParseInt(string? value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static float ParseSingle(string? value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
        }

        private static string? SerializePayload(object? payload)
        {
            return payload is null ? null : JsonConvert.SerializeObject(payload);
        }

        private async Task<Reservation> EnsureStayWellLinkInExternalNoteAsync(
            Reservation reservation,
            Guid reservationGuid,
            Guid bookingComLogGuid,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(reservation);

            var stayWellLink = BuildStayWellLink(reservationGuid.ToString("D"));
            if (string.IsNullOrWhiteSpace(stayWellLink))
            {
                await LogWarningAsync(
                    bookingComLogGuid,
                    "staywell_link_missing",
                    $"StayWell link could not be built for reservation {reservation.id}. Skipped ExternalNote update.",
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                return reservation;
            }

            reservation.ReservationDetails ??= new ReservationDetails();

            var existingExternalNote = reservation.ReservationDetails.externalNote ?? string.Empty;
            var expectedPrefix = $"{stayWellLink}";

            if (existingExternalNote.Contains(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await LogInfoAsync(
                    bookingComLogGuid,
                    "staywell_link_already_present",
                    "Skipped",
                    $"StayWell link was already present in ExternalNote for reservation {reservation.id}.",
                    reservationGuid: reservationGuid,
                    cancellationToken: cancellationToken);

                return reservation;
            }

            var updatedExternalNote = string.IsNullOrWhiteSpace(existingExternalNote)
                ? expectedPrefix
                : $"{expectedPrefix}{Environment.NewLine}{Environment.NewLine}{existingExternalNote.Trim()}";

            var editResponse = await _idoApi.EditReservationAsync(
                new EditReservation
                {
                    Id = reservation.id,
                    ExternalNote = updatedExternalNote,
                 //   Notify = ReservationNotifyType.No,
                  //  NotifyService = ReservationNotifyType.No
                },
                cancellationToken);

            var editResult = editResponse?.Reservations?.FirstOrDefault();
            if (editResult?.Success != true)
            {
                var errorMessage = editResult?.Error?.FaultString
                    ?? editResponse?.Errors?.FaultString
                    ?? $"Unknown error while updating ExternalNote for reservation {reservation.id}.";

                throw new InvalidOperationException(errorMessage);
            }

            reservation.ReservationDetails.externalNote = updatedExternalNote;

            await LogInfoAsync(
                bookingComLogGuid,
                "staywell_link_appended",
                "Completed",
                $"Prepended StayWell link to ExternalNote for reservation {reservation.id}.",
                payload: new
                {
                    reservation.id,
                    StayWellLink = stayWellLink
                },
                reservationGuid: reservationGuid,
                cancellationToken: cancellationToken);

            return reservation;
        }

        private string? BuildStayWellLink(string? resToken)
        {
            var baseUrl =
                Environment.GetEnvironmentVariable("StayWell__ReservationUrlBase") ??
                Environment.GetEnvironmentVariable("StayWellReservationUrlBase") ??
                _configuration["StayWell:ReservationUrlBase"] ??
                _configuration["StayWellReservationUrlBase"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(resToken))
            {
                return null;
            }

            return baseUrl.Replace("{resToken}", resToken).TrimEnd('/');
        }

        private bool IsDevelopmentEnvironment()
        {
            var environmentName =
                _configuration["AZURE_FUNCTIONS_ENVIRONMENT"]
                ?? _configuration["DOTNET_ENVIRONMENT"]
                ?? Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

            return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
        }
    }
}
