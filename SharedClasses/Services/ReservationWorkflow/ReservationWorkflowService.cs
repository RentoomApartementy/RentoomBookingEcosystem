using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Bonuses;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow
{

    public interface IReservationWorkflowService
    {
        Task<Guid> StartAsync(StartReservationRequest request);
        Task UpdateStartRequestAsync(Guid reservationGuid, StartReservationRequest request);
        Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice);
        Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid);
        Task<ReservationSummaryDto> BuildDraftSummaryAsync(Guid reservationGuid);
        Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid);
        Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid);
        Task<PaymentStateDto> VerifyPaymentAfterErrorReturnAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
        Task HandleTpayWebhookAsync(TpayWebhookDto dto);
        Task EnsureIdoPaymentAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
        Task<RentoomReservation?> EnsureRentoomReservationByResTokenAsync(string resToken, CancellationToken cancellationToken = default);
        Task MarkPaymentAsPaidAsync(Guid reservationGuid); //for dummy scenario
        Task<DealEmailStatusDto> GetDealEmailStatusAsync(Guid reservationGuid);
        Task SaveCustomerTermsAsync(Guid reservationGuid, Dictionary<int, bool> termSelections);
        Task CancelReservationAsync(Guid reservationGuid, CancellationToken cancellationToken = default);
    }

    public class ReservationWorkflowService : IReservationWorkflowService, IReservationWorkflowSyncOperations
    {
        private readonly IReservationStore _store;
        private readonly ApartmentRepository _apartmentStore;
        private readonly IdoSellService _idoApi;
        private readonly PostgresBookingDatabase _bookingDatabase;
        private readonly ITpayGateway _tpayGateway;
        private readonly BitrixService _bitrixService;
        private readonly RappQrMaintService _qrMaintService;
        private readonly IConfiguration _configuration;

        private readonly IUpsellCatalogService _upsellCatalogService;
        private readonly IUpsellOrderWorkflowService _upsellOrderWorkflowService;
        private readonly IBonusesService _bonusesService;


        private readonly CustomerTermsRepository _termsRepository;
        private readonly ILogger<ReservationWorkflowService> _logger;
        private readonly int _bitrixAssignedByUserId;
        private const string BitrixStayWellLinkFieldName = "UF_CRM_1768835603310";
        private const string BitrixApartmentGoogleMapsFieldName = "UF_CRM_1773873147169";
        private const string BitrixParkingMapFieldName = "UF_CRM_1774428026254";
        private const string BitrixPurchasedAddonsFieldName = "UF_CRM_1768940634224";
        private const string BitrixReservationSourceFieldName = "UF_CRM_1774626264627";
        private const string RentoomBookingWebReservationSourceValue = "Direct Selling_api";
        public ReservationWorkflowService(
            IReservationStore store,
            IdoSellService idoApi,
            PostgresBookingDatabase bookingDatabase,
            ITpayGateway tpayGateway,
            BitrixService bitrixService,
            RappQrMaintService qrMaintService,
            IConfiguration configuration,
            ApartmentRepository apartmentStore,
             IUpsellCatalogService upsellCatalogService,
             IUpsellOrderWorkflowService upsellOrderWorkflowService,
             IBonusesService bonusesService,
            CustomerTermsRepository termsRepository,
        ILogger<ReservationWorkflowService> logger)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _idoApi = idoApi ?? throw new ArgumentNullException(nameof(idoApi));
            _bookingDatabase = bookingDatabase ?? throw new ArgumentNullException(nameof(bookingDatabase));
            _tpayGateway = tpayGateway ?? throw new ArgumentNullException(nameof(tpayGateway));
            _bitrixService = bitrixService ?? throw new ArgumentNullException(nameof(bitrixService));
            _qrMaintService = qrMaintService ?? throw new ArgumentNullException(nameof(qrMaintService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apartmentStore = apartmentStore ?? throw new ArgumentNullException(nameof(apartmentStore));
            _upsellCatalogService = upsellCatalogService ?? throw new ArgumentNullException(nameof(upsellCatalogService));
            _upsellOrderWorkflowService = upsellOrderWorkflowService ?? throw new ArgumentNullException(nameof(upsellOrderWorkflowService));
            _bonusesService = bonusesService ?? throw new ArgumentNullException(nameof(bonusesService));
            _termsRepository = termsRepository;
            _bitrixAssignedByUserId = BitrixConfiguration.GetAssignedByUserId(configuration);
        }



private static TimeZoneInfo GetWarsawTimeZone()
    {
        try
        {
            // Linux / Azure Linux
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }

    private static TimeSpan GetWarsawOffset(DateOnly date, TimeOnly time)
    {
        var tz = GetWarsawTimeZone();
        var localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        return tz.GetUtcOffset(localDateTime);
    }

    private static string ToBitrixDateTime(DateOnly? date, TimeOnly? time, TimeSpan? offset = null, double hoursdiff = 0)
    {
        if (date is null || time is null)
            throw new ArgumentNullException($"Bitrix datetime requires both date and time.");

        var localDateTime = date.Value.ToDateTime(time.Value, DateTimeKind.Unspecified);
            localDateTime = localDateTime.AddHours(hoursdiff);
        var effectiveOffset = offset ?? GetWarsawOffset(date.Value, time.Value);
        var dto = new DateTimeOffset(localDateTime, effectiveOffset);

        return dto.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private async Task<string?> ResolveBitrixReservationSourceValueAsync(
        ReservationRecord record,
        Reservation? idoReservation = null,
        CancellationToken cancellationToken = default)
    {
        if (!record.IdoReservationId.HasValue)
        {
            return RentoomBookingWebReservationSourceValue;
        }

        idoReservation ??= await FetchIdoReservationAsync(record, refreshCache: false, cancellationToken);
        var details = idoReservation?.ReservationDetails;
            if (details is null || details.reservationSourceTypeId <= 0 || string.IsNullOrWhiteSpace(details.reservationSourceId))
            {
                return RentoomBookingWebReservationSourceValue;
            }

            if ( Convert.ToInt32(details.reservationSourceId) == 2) //direct selling api
            {
                return RentoomBookingWebReservationSourceValue;
            }

            var reservationSources = await _idoApi.GetReservationSourcesAsync(
            new ReservationSourcesResultRequest
            {
                Page = 1,
                Number = 100
            },
            cancellationToken);

        var sources = reservationSources?.Sources;
        if (sources is null || sources.Count == 0)
        {
            return null;
        }

        if (!int.TryParse(details.reservationSourceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reservationSourceId))
        {
            return null;
        }

        var source = sources.FirstOrDefault(s =>
            s.ReservationSourceTypeId == details.reservationSourceTypeId &&
            s.ReservationSourceId == reservationSourceId);

        return source is null
            ? null
            : $"{source.ReservationSourceTypeName}_{source.ReservationSourceName}";
    }

    private ApartmentObject? ResolveApartmentInfo(StartReservationRequest? startRequest, Reservation? idoReservation = null)
    {
        if (startRequest?.ObjectId > 0) //najpierw sprawdz start request , bo w przypadku rezerwacji z Idobooking to on jest bardziej wiarygodny niż dane z rezerwacji ido (czasem w ido rezerwacja jest tworzona zanim Idobooking przekaże wszystkie dane, wtedy w ido może być tylko id obiektu bez szczegółów, a w start request są już pełne dane)
            {
            return _apartmentStore.FindApartmentInPostgres(startRequest.ObjectId);
        }

        var reservationItem = idoReservation?.Items?.FirstOrDefault();
        if (reservationItem?.objectId > 0)
        {
            return _apartmentStore.FindApartmentInPostgres(reservationItem.objectId);
        }

        if (reservationItem?.objectItemId > 0)
        {
            return _apartmentStore.FindApartmentByItemId(reservationItem.objectItemId);
        }

        return null;
    }

    private async Task<ApartmentItemLocalSettings?> ResolveApartmentItemLocalSettingsAsync(
        StartReservationRequest? startRequest,
        Reservation? idoReservation = null,
        CancellationToken cancellationToken = default)
    {
        var apartmentItemId = startRequest?.ObjectItemId;
        if ((!apartmentItemId.HasValue || apartmentItemId.Value <= 0) && idoReservation?.Items?.FirstOrDefault() is { } reservationItem)
        {
            if (reservationItem.objectItemId > 0)
            {
                apartmentItemId = reservationItem.objectItemId;
            }
            else if (reservationItem.itemId > 0)
            {
                apartmentItemId = reservationItem.itemId;
            }
        }

        if (!apartmentItemId.HasValue || apartmentItemId.Value <= 0)
        {
            return null;
        }

        return await _qrMaintService.GetApartmentItemCodesAsync(apartmentItemId.Value, cancellationToken);
    }

    private static string? BuildApartmentGoogleMapsUrl(ApartmentObject? apartmentInfo)
    {
        var location = apartmentInfo?.ObjectLocation?.LocalizationItem;
        if (location?.GeoLocationLat is null || location.GeoLocationLng is null)
        {
            return null;
        }

        var latitude = location.GeoLocationLat.Value.ToString(CultureInfo.InvariantCulture);
        var longitude = location.GeoLocationLng.Value.ToString(CultureInfo.InvariantCulture);
        return $"https://www.google.com/maps?q={latitude},{longitude}";
    }

    private static void AddBitrixLocationFields(
        IDictionary<string, object?> fields,
        ApartmentObject? apartmentInfo,
        ApartmentItemLocalSettings? apartmentItemLocalSettings)
    {
        var apartmentGoogleMapsUrl = BuildApartmentGoogleMapsUrl(apartmentInfo);
        if (!string.IsNullOrWhiteSpace(apartmentGoogleMapsUrl))
        {
            fields[BitrixApartmentGoogleMapsFieldName] = apartmentGoogleMapsUrl;
        }

        if (!string.IsNullOrWhiteSpace(apartmentItemLocalSettings?.ParkingMapUrl))
        {
            fields[BitrixParkingMapFieldName] = apartmentItemLocalSettings.ParkingMapUrl;
        }
    }

    private static bool UpdateReservationLocationState(
        ReservationRecord record,
        ApartmentObject? apartmentInfo,
        ApartmentItemLocalSettings? apartmentItemLocalSettings)
    {
        var apartmentGoogleMapsUrl = BuildApartmentGoogleMapsUrl(apartmentInfo) ?? string.Empty;
        var parkingMapUrl = apartmentItemLocalSettings?.ParkingMapUrl?.Trim() ?? string.Empty;

        var changed =
            !string.Equals(record.State.GoogleMapsLink, apartmentGoogleMapsUrl, StringComparison.Ordinal) ||
            !string.Equals(record.State.ParkingMapUrl, parkingMapUrl, StringComparison.Ordinal);

        record.State.GoogleMapsLink = apartmentGoogleMapsUrl;
        record.State.ParkingMapUrl = parkingMapUrl;
        return changed;
    }

    public async Task<Guid> StartAsync(StartReservationRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var record = await _store.CreateAsync(request);
            return record.ReservationGuid; //<== to jest tez reservation token dla staywell
        }

        public async Task UpdateStartRequestAsync(Guid reservationGuid, StartReservationRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            var record = await RequireReservationAsync(reservationGuid);
            record.State.StartRequest = request;
            await _store.UpdateAsync(record);
        }

        public async Task SaveClientInfoAsync(Guid reservationGuid, ClientInfoDto client, InvoiceInfoDto? invoice)
        {
            var record = await RequireReservationAsync(reservationGuid); //sprawdz czy rezerwacja istnieje
            record.State.Client = client ?? throw new ArgumentNullException(nameof(client));
            record.State.Client.Language = NormalizeIdoLanguage(record.State.Client.Language);
            record.State.Invoice = invoice;
            await _store.UpdateAsync(record);

          //  record = await EnsureBitrixContactAndDealAsync(record);
          //  await UpdateBitrixDealAsync(record, "Client info updated");
        }

        public async Task<ReservationSummaryDto> BuildSummaryAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            await EnsurePaymentTotalsAsync(reservationGuid, record); //synchronnie uaktualnij kwoty w rekordzie przed zbudowaniem podsumowania, aby mieć pewność że są aktualne
            

            if (record.IdoStatus != ReservationStatusType.Accepted && record.PaymentStatus != PaymentStatuses.Paid)
            {
                record = await EnsureIdoReservationAsync(record, ReservationStatusType.Accepted);
                record = await EnsureBitrixContactAndDealAsync(record);
                //record.IdoStatus = ReservationStatusType.WaitingForPayment; <<usuniete bo link do retry platnosci wchodzil za pozno

                await _store.UpdateAsync(record);
                //await UpdateIdoStatusAsync(record, ReservationStatusType.WaitingForPayment); <<usuniete bo link do retry platnosci wchodzil za pozno
                //await UpdateBitrixDealAsync(record, "BuildSummaryAsync Update"); <<usuniete bo link do retry platnosci wchodzil za pozno

                record = await RequireReservationAsync(reservationGuid);
            }
            return await BuildSummaryFromRecordAsync(reservationGuid, record);
        }

        public async Task<ReservationSummaryDto> BuildDraftSummaryAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            return await BuildSummaryFromRecordAsync(reservationGuid, record);
        }

     
        private async Task<ReservationSummaryDto> BuildSummaryFromRecordAsync(Guid reservationGuid, ReservationRecord record)
        {
            var startRequest = record.State.StartRequest;
            var upsellLines = new List<UpsellSummaryLineDto>();
            var upsellsTotal = 0m;

            if (startRequest?.SelectedUpsells?.Count > 0)
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                var tiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(startRequest.ObjectId, culture,"all");
                
                var tileLookup = tiles.ToDictionary(tile => tile.PartnerServiceId);
                
                var pricingContext = new ReservationPricingContext
                {
                    StartDate = startRequest.StartDate,
                    EndDate = startRequest.EndDate,
                    Adults = startRequest.Adults,
                    Children = startRequest.Children,
                    Currency = startRequest.Currency ?? "PLN"
                };

                foreach (var selected in startRequest.SelectedUpsells)
                {
                    if (!tileLookup.TryGetValue(selected.PartnerServiceId, out var tile))
                    {
                        continue;
                    }

                    var quantity = Math.Max(1, selected.Quantity);
                    
                    
                    var lineTotal = UpsellPricingCalculator.CalculateTotal(
                        tile.PricingModel,
                        tile.Price,
                        pricingContext.Nights,
                        pricingContext.TotalGuests,
                        quantity);

                    upsellLines.Add(new UpsellSummaryLineDto
                    {
                        PartnerServiceId = tile.PartnerServiceId,
                        Title = tile.Title,
                        PricingModel = tile.PricingModel,
                        Quantity = quantity,
                        UnitPriceGross = tile.Price,
                        Nights = pricingContext.Nights,
                        TotalGuests = pricingContext.TotalGuests,
                        LineTotalGross = lineTotal,
                        DisplayText = $"{tile.Price} {tile.Currency}"
                    });

                    upsellsTotal += lineTotal;
                }
            }


            var addonsLines = new List<AddonSummaryLineDto>();
            decimal addonsTotal = 0m;//startRequest?.SelectedAddons?.Sum(addon => (decimal)addon.Price * addon.Quantity) ?? 0m; //wyliczenie do sprawdzenia bo nie bierze pod uwage typu addonu
            var addonsDisplayLookup = new Dictionary<int, string>();

            if (startRequest?.SelectedAddons?.Any(a => string.IsNullOrWhiteSpace(a.DisplayText)) == true)
            {
                var cultureAddonLang = ResolveAddonDetailsLanguage(CultureInfo.CurrentUICulture.Name);
                var definedAddons = await _apartmentStore.GetDefinedAddonsAsync();
                addonsDisplayLookup = definedAddons.ToDictionary(
                    addon => addon.IdoBookingId,
                    addon => addon.AddonDefinition?.Details?
                                 .FirstOrDefault(d => string.Equals(d.Lang, cultureAddonLang, StringComparison.OrdinalIgnoreCase))?.Name
                             ?? addon.AddonDefinition?.Details?.FirstOrDefault()?.Name
                             ?? addon.Name);
            }

            foreach (var addon in startRequest?.SelectedAddons ?? [])
            {
                var addonDisplayText = !string.IsNullOrWhiteSpace(addon.DisplayText)
                    ? addon.DisplayText
                    : addonsDisplayLookup.GetValueOrDefault(addon.AddonId) ?? string.Empty;

                var addonPrice = AddonPricingCalculator.CalculateTotal(addon.PaymentType, (decimal)addon.Price, addon.Nights, startRequest.Adults + startRequest.Children, addon.Quantity);

                addonsLines.Add(new AddonSummaryLineDto
                {
                    AddonId = addon.AddonId,
                    DisplayText = addonDisplayText,
                    LineTotalGross = addonPrice,
                    Nights = addon.Nights,
                    PaymentType = addon.PaymentType,
                    Persons = addon.Persons,
                    Price = addon.Price,
                    Quantity = addon.Quantity,
                    Vat = addon.Vat,
                });


                addonsTotal += addonPrice;



            }

            var offerPrice = startRequest?.OfferPrice ?? 0m;
            var bonusResult = await EvaluateBonusAsync(startRequest, offerPrice);

            if (startRequest is not null)
            {
                startRequest.BonusInputName = bonusResult.NormalizedBonusInputName;
                startRequest.AppliedBonusId = bonusResult.AppliedBonusId;
                startRequest.AppliedBonusName = bonusResult.AppliedBonusName;
                startRequest.AppliedBonusValueType = bonusResult.AppliedBonusValueType;
                startRequest.AppliedBonusValue = bonusResult.AppliedBonusValue;
                startRequest.BonusBasePln = bonusResult.BonusBasePln;
                startRequest.DiscountAmountPln = bonusResult.DiscountAmountPln;
                startRequest.BonusRejectReason = string.Equals(bonusResult.RejectReason, "empty", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : bonusResult.RejectReason;
            }

            decimal grandTotalBeforeDiscount = offerPrice + addonsTotal + upsellsTotal;
            decimal discountAmount = Math.Min(bonusResult.DiscountAmountPln, grandTotalBeforeDiscount);
            decimal grandTotal = Math.Max(0m, grandTotalBeforeDiscount - discountAmount);

            return new ReservationSummaryDto
            {
                ReservationGuid = reservationGuid,
                StartRequest = startRequest,
                Client = record.State.Client,
                Invoice = record.State.Invoice,
                IdoReservationId = record.IdoReservationId,
                IdoStatus = record.IdoStatus,
                OfferPrice = startRequest?.OfferPrice, //<<- czysta kwota za rezerwację idąca do Idobooking (bez upselli
                Currency = startRequest?.Currency ?? "PLN",
                PaymentStatus = record.PaymentStatus,
                Upsells = upsellLines,
                Addons = addonsLines,
                AddonsTotal = addonsTotal,
                UpsellsTotal = upsellsTotal,
                GrandTotalBeforeDiscount = grandTotalBeforeDiscount,
                DiscountAmountPln = discountAmount,
                AppliedBonusName = bonusResult.AppliedBonusName,
                BonusRejectReason = string.Equals(bonusResult.RejectReason, "empty", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : bonusResult.RejectReason,
                GrandTotal = grandTotal
            };
        }

        private async Task<BonusCalculationResult> EvaluateBonusAsync(StartReservationRequest? startRequest, decimal offerPrice)
        {
            if (startRequest is null)
            {
                return new BonusCalculationResult();
            }

            return await _bonusesService.EvaluateAsync(new BonusCalculationRequest
            {
                BonusInputName = startRequest.BonusInputName,
                BookingChannel = startRequest.BookingChannel,
                ReservationStartDate = startRequest.StartDate,
                ApartmentItemId = startRequest.ObjectItemId,
                OfferPrice = offerPrice,
                MandatoryAddonsTotalPrice = startRequest.MandatoryAddonsTotalPrice
            });
        }

        public async Task EnsurePaymentTotalsAsync(Guid reservationGuid, ReservationRecord record)
        {
            var summary = await BuildSummaryFromRecordAsync(reservationGuid, record);
            var updated = false;
            var startRequest = record.State.StartRequest;
            var summaryStartRequest = summary.StartRequest;

            if (startRequest is not null && summaryStartRequest is not null)
            {
                if (startRequest.AppliedBonusId != summaryStartRequest.AppliedBonusId
                    || !string.Equals(startRequest.AppliedBonusName, summaryStartRequest.AppliedBonusName, StringComparison.Ordinal)
                    || startRequest.AppliedBonusValueType != summaryStartRequest.AppliedBonusValueType
                    || startRequest.AppliedBonusValue != summaryStartRequest.AppliedBonusValue
                    || startRequest.BonusBasePln != summaryStartRequest.BonusBasePln
                    || startRequest.DiscountAmountPln != summaryStartRequest.DiscountAmountPln
                    || !string.Equals(startRequest.BonusRejectReason, summaryStartRequest.BonusRejectReason, StringComparison.Ordinal)
                    || !string.Equals(startRequest.BonusInputName, summaryStartRequest.BonusInputName, StringComparison.Ordinal))
                {
                    startRequest.BonusInputName = summaryStartRequest.BonusInputName;
                    startRequest.AppliedBonusId = summaryStartRequest.AppliedBonusId;
                    startRequest.AppliedBonusName = summaryStartRequest.AppliedBonusName;
                    startRequest.AppliedBonusValueType = summaryStartRequest.AppliedBonusValueType;
                    startRequest.AppliedBonusValue = summaryStartRequest.AppliedBonusValue;
                    startRequest.BonusBasePln = summaryStartRequest.BonusBasePln;
                    startRequest.DiscountAmountPln = summaryStartRequest.DiscountAmountPln;
                    startRequest.BonusRejectReason = summaryStartRequest.BonusRejectReason;
                    updated = true;
                }
            }

            if (record.State.PaymentUpsellsTotal != summary.UpsellsTotal)
            {
                record.State.PaymentUpsellsTotal = summary.UpsellsTotal;
                updated = true;
            }

            if (record.State.PaymentGrandTotal != summary.GrandTotal)
            {
                record.State.PaymentGrandTotal = summary.GrandTotal;
                updated = true;
            }

            if (updated)
            {
                await _store.UpdateAsync(record);
            }
        }




        public async Task<PaymentInitResult> InitiatePaymentAsync(Guid reservationGuid)
        {
            while (true)
            {
                var record = await RequireReservationAsync(reservationGuid);
                record = await EnsureIdoReservationAsync(record, ReservationStatusType.Accepted); //<< jako accepted bo waitingForPayment jest za szybko.
                record = await EnsureBitrixContactAndDealAsync(record); //<< jako accepted bo waitingForPayment jest za szybko i nie pojdzie link retry platnosci

                if (record.PaymentStatus == PaymentStatuses.Paid && record.PaymentSessionGuid.HasValue)
                {
                    await EnsurePaymentTotalsAsync(reservationGuid, record);
                    //var redirectUrl = record.State.PaymentRedirectUrl ?? $"/rezerwuj/{reservationGuid}/podsumowanie";
                    var redirectUrl = $"/rezerwuj/{reservationGuid}/podsumowanie";


                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = record.PaymentSessionGuid.Value,
                        ProviderTransactionId = record.ProviderTransactionId ?? string.Empty,
                        RedirectUrl = redirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }

                if (record.PaymentStatus == PaymentStatuses.Initiated && record.PaymentSessionGuid.HasValue)
                {
                    await EnsurePaymentTotalsAsync(reservationGuid, record);
                    //await UpdateBitrixPaymentRetryLinkAsync(record, record.PaymentSessionGuid.Value);
                    var redirectUrl = record.State.PaymentRedirectUrl;

                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = record.PaymentSessionGuid.Value,
                        ProviderTransactionId = record.ProviderTransactionId ?? string.Empty,
                        RedirectUrl = redirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }

                var paymentSessionGuid = Guid.NewGuid();

                var summary = await BuildSummaryFromRecordAsync(reservationGuid, record);
                var amount = summary.GrandTotal;

                var currency = record.State.StartRequest?.Currency ?? "PLN";

                var paymentResult = await _tpayGateway.CreatePaymentAsync(reservationGuid, paymentSessionGuid, amount, currency, record.IdoReservationId);
                if (!paymentResult.Success)
                {
                    throw new InvalidOperationException("Failed to initiate payment session: " + paymentResult.Message);
                }

                record.PaymentSessionGuid = paymentSessionGuid;
                record.PaymentStatus = PaymentStatuses.Initiated;
                record.Provider = record.Provider ?? "TPAY";
                record.ProviderTransactionId = paymentResult.TransactionId;
                record.State.PaymentRedirectUrl = paymentResult.RedirectUrl;
                record.State.ProviderTransactionUid = paymentResult.TransactionUid;
                record.State.PaymentUpsellsTotal = summary.UpsellsTotal;
                record.State.PaymentGrandTotal = summary.GrandTotal;
                record.IdoStatus = ReservationStatusType.WaitingForPayment; //<< dopiero too

                try
                {
                    await _store.UpdateAsync(record);
                    await UpdateBitrixPaymentRetryLinkAsync(record, paymentSessionGuid); //<< najpierw retry link dodajemy
                    await UpdateIdoStatusAsync(record, ReservationStatusType.WaitingForPayment); //<< status w ido
                    //await AddIdoPaymentAsync(record, amount, currency, paymentResult.TransactionId);
                    await UpdateBitrixDealAsync(record, "ReservationWorkflowService - InitiatePaymentAsync - Payment initiated"); //<< update deal do waiting to payment z już obecnym linkiem retry payment - waitingforpayment przesunie automatyzacjie bitrix do"czeka na platnosci" juz z linkiem retry do maila.

                    return new PaymentInitResult
                    {
                        ReservationGuid = reservationGuid,
                        PaymentSessionGuid = paymentSessionGuid,
                        ProviderTransactionId = paymentResult.TransactionId,
                        RedirectUrl = paymentResult.RedirectUrl,
                        Provider = record.Provider ?? "TPAY"
                    };
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while initiating payment for {ReservationGuid}. Retrying.", reservationGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }

        private async Task UpdateBitrixPaymentRetryLinkAsync(ReservationRecord record, Guid paymentSessionGuid)
        {
            if (!record.DealBitrixId.HasValue)
            {
                return;
            }

            var paymentRetryLink = BuildPaymentRetryLink(record.ReservationGuid, paymentSessionGuid, record.Provider);
            if (string.IsNullOrWhiteSpace(paymentRetryLink))
            {
                return;
            }

            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, new Dictionary<string, object?>
            { //RB_Link_Do_Platnosci
                ["UF_CRM_1775071642554"] = paymentRetryLink
            });
        }

        private async Task<int?> AddIdoPaymentAsync(ReservationRecord record, decimal amount, string currency, string? transactionId)
        {
            if (record.IdoReservationId is null || string.IsNullOrWhiteSpace(transactionId))
            {
                return null;
            }

            var payment = new PaymentAdd
            {
                ReservationId = record.IdoReservationId.Value,
                Value = Convert.ToSingle(amount),
                Currency = currency,
                ExternalPaymentId = transactionId,
            };

            var paymentresult = await _idoApi.AddPaymentAsync(payment);
            return paymentresult?.Results[0].Id;
        }

        public async Task EnsureIdoPaymentAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var record = await RequireReservationAsync(reservationGuid);
            if (!string.Equals(record.PaymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (record.IdoReservationId is null || string.IsNullOrWhiteSpace(record.ProviderTransactionId))
            {
                _logger.LogWarning("Cannot ensure IdoBooking payment for reservation {ReservationGuid}. Missing IdoReservationId or ProviderTransactionId.", reservationGuid);
                return;
            }

            var idoReservation = await FetchIdoReservationAsync(record, refreshCache: false, cancellationToken);
            var currentIdoStatus = idoReservation?.ReservationDetails?.status;
            if (!string.IsNullOrWhiteSpace(currentIdoStatus) && !string.Equals(record.IdoStatus, currentIdoStatus, StringComparison.OrdinalIgnoreCase))
            {
                record.IdoStatus = currentIdoStatus;
                await _store.UpdateAsync(record, cancellationToken);
            }

            var existingPayment = await GetExistingIdoPaymentByTransactionIdAsync(record, record.ProviderTransactionId!, cancellationToken);
            var paymentId = existingPayment?.Id;

            if (!paymentId.HasValue)
            {
                paymentId = await AddIdoPaymentAsync(
                    record,
                    //record.State.StartRequest?.OfferPrice + record.State.StartRequest?.SelectedAddonsTotalPrice ?? 0m,
                    record.State.StartRequest.getFullReservationPrizeWithoutUpsells(),
                    record.State.StartRequest?.Currency ?? "PLN",
                    record.ProviderTransactionId);
            }

            if (paymentId.HasValue && !string.Equals(existingPayment?.Status, PaymentStatus.Processed, StringComparison.OrdinalIgnoreCase))
            {
                await ConfirmIdoPaymentAsync(paymentId.Value, cancellationToken);
            }

            if (string.Equals(currentIdoStatus, ReservationStatusType.WaitingForPayment, StringComparison.OrdinalIgnoreCase))
            {
                await UpdateIdoStatusAsync(record, ReservationStatusType.Accepted);
                idoReservation = await FetchIdoReservationAsync(record, refreshCache: true, cancellationToken);
                currentIdoStatus = idoReservation?.ReservationDetails?.status ?? ReservationStatusType.Accepted;
            }
            else if (paymentId.HasValue)
            {
                await FetchIdoReservationAsync(record, refreshCache: true, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(currentIdoStatus) && !string.Equals(record.IdoStatus, currentIdoStatus, StringComparison.OrdinalIgnoreCase))
            {
                record.IdoStatus = currentIdoStatus;
                await _store.UpdateAsync(record, cancellationToken);
            }

            record = await EnsureBitrixContactAndDealAsync(record);
            //await UpdateBitrixDealAsync(record, "ReservationWorkflowService - EnsureIdoPaymentAsync - Ido payment synchronized");
        }

        public async Task<RentoomReservation?> EnsureRentoomReservationByResTokenAsync(string resToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(resToken))
            {
                throw new ArgumentNullException(nameof(resToken));
            }

            ReservationRecord? reservationRecord = null;
            Guid reservationGuid = Guid.Empty;

            if (Guid.TryParse(resToken, out reservationGuid))
            {
                reservationRecord = await _store.GetAsync(reservationGuid, cancellationToken);
                if (reservationRecord is not null)
                {
                    //await EnsureIdoPaymentAsync(reservationGuid, cancellationToken);<< nadpisywało podwójnie 
                    reservationRecord = await _store.GetAsync(reservationGuid, cancellationToken);
                }
            }

            var normalizedCandidates = BuildReservationTokenCandidates(resToken);

            foreach (var candidate in normalizedCandidates)
            {
                var existingReservation = await _bookingDatabase.GetRentoomReservationByResTokenAsync(candidate, _logger, cancellationToken);
                if (existingReservation is not null)
                {
                    return existingReservation;
                }
            }

            if (reservationGuid == Guid.Empty)
            {
                _logger.LogWarning("Reservation token {ReservationToken} is not a valid GUID and no cached reservation was found.", resToken);
                return null;
            }

            if (reservationRecord is null)
            {
                _logger.LogWarning("Reservation record for token {ReservationToken} was not found in reservation_records.", resToken);
                return null;
            }

            return await FetchReservationDocumentForRecordAsync(reservationRecord, reservationGuid, cancellationToken);
        }

        private static string MapIdoPaymentStatus(string paymentStatus)
        {
            return paymentStatus switch
            {
                PaymentStatuses.Paid => PaymentStatus.Processed,
                PaymentStatuses.Failed => PaymentStatus.Cancelled,
                PaymentStatuses.Initiated => PaymentStatus.Pending,
                _ => PaymentStatus.Pending
            };
        }

       


        public async Task<PaymentStateDto> GetPaymentStateAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);
            return new PaymentStateDto
            {
                ReservationGuid = reservationGuid,
                PaymentStatus = record.PaymentStatus,
                PaymentSessionGuid = record.PaymentSessionGuid,
                ProviderTransactionId = record.ProviderTransactionId,
                ProviderTransactionUid = record.State.ProviderTransactionUid,
                Provider = record.Provider,
                RedirectUrl = record.State.PaymentRedirectUrl,
                IdoStatus = record.IdoStatus
            };
        }

        public async Task<PaymentStateDto> VerifyPaymentAfterErrorReturnAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var initialState = await GetPaymentStateAsync(reservationGuid);
            if (IsFinalPaymentStatus(initialState.PaymentStatus))
            {
                return initialState;
            }

            var record = await RequireReservationAsync(reservationGuid, cancellationToken);
            var transactionUid = record.State.ProviderTransactionUid;
            if (string.IsNullOrWhiteSpace(transactionUid))
            {
                return initialState;
            }

            var tpayState = await _tpayGateway.GetPaymentStatusAsync(transactionUid, cancellationToken);
            if (!tpayState.Success)
            {
                _logger.LogWarning("Tpay fallback verification failed for reservation {ReservationGuid}: {Message}",
                    reservationGuid,
                    tpayState.Message);

                return await GetPaymentStateAsync(reservationGuid);
            }

            var mappedStatus = MapTpayStatusToWorkflowStatus(tpayState.TransactionStatus, tpayState.AmountPaid);
            if (!string.Equals(mappedStatus, PaymentStatuses.Initiated, StringComparison.OrdinalIgnoreCase)
                && record.PaymentSessionGuid.HasValue
                && !string.IsNullOrWhiteSpace(record.ProviderTransactionId))
            {
                await HandleTpayWebhookAsync(new TpayWebhookDto
                {
                    ReservationGuid = reservationGuid,
                    PaymentSessionGuid = record.PaymentSessionGuid.Value,
                    ProviderTransactionId = record.ProviderTransactionId,
                    Status = string.Equals(mappedStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase) ? "PAID" : "FAILED",
                    Signature = "fallback-check"
                });
            }

            return await GetPaymentStateAsync(reservationGuid);
        }

        private static bool IsFinalPaymentStatus(string? paymentStatus)
        {
            return string.Equals(paymentStatus, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentStatus, PaymentStatuses.Failed, StringComparison.OrdinalIgnoreCase);
        }

        private static string MapTpayStatusToWorkflowStatus(string? tpayStatus, decimal? amountPaid)
        {
            if (amountPaid.GetValueOrDefault() > 0m)
            {
                return PaymentStatuses.Paid;
            }

            if (string.IsNullOrWhiteSpace(tpayStatus))
            {
                return PaymentStatuses.Initiated;
            }

            if (string.Equals(tpayStatus, "pending", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatuses.Initiated;
            }

            if (string.Equals(tpayStatus, "correct", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tpayStatus, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tpayStatus, "success", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentStatuses.Paid;
            }

            return PaymentStatuses.Failed;
        }

        public async Task CancelReservationAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var record = await RequireReservationAsync(reservationGuid, cancellationToken);
            if (!record.IdoReservationId.HasValue)
            {
                throw new InvalidOperationException($"Reservation {reservationGuid} does not have IdoReservationId.");
            }

            if (string.Equals(record.IdoStatus, ReservationStatusType.Canceled, StringComparison.OrdinalIgnoreCase))
            {
                if (record.DealBitrixId.HasValue)
                {
                    await UpdateBitrixDealAsync(record, "Reservation canceled");
                }

                return;
            }

            await UpdateIdoStatusAsync(record, ReservationStatusType.Canceled);
            record.IdoStatus = ReservationStatusType.Canceled;
            await _store.UpdateAsync(record, cancellationToken);

            if (!record.DealBitrixId.HasValue && record.State.Client is not null)
            {
                record = await EnsureBitrixContactAndDealAsync(record);
            }

            if (record.DealBitrixId.HasValue)
            {
                await UpdateBitrixDealAsync(record, "Reservation canceled");
            }
        }

        public async Task HandleTpayWebhookAsync(TpayWebhookDto dto)
        {
            if (dto is null) throw new ArgumentNullException(nameof(dto));

            while (true)
            {
                var record = await RequireReservationAsync(dto.ReservationGuid);
                await EnsurePaymentTotalsAsync(record.ReservationGuid, record);
                record = await EnsureBitrixContactAndDealAsync(record);
                if (record.PaymentSessionGuid != dto.PaymentSessionGuid)
                {
                    _logger.LogWarning("Payment session guid mismatch for reservation {ReservationGuid}.", dto.ReservationGuid);
                    throw new InvalidOperationException("Payment session mismatch.");
                }

                if (!string.Equals(record.ProviderTransactionId, dto.ProviderTransactionId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Provider transaction id mismatch for reservation {ReservationGuid}.", dto.ReservationGuid);
                    throw new InvalidOperationException("Transaction mismatch.");
                }

                if (string.IsNullOrWhiteSpace(record.PaymentStatus) || record.PaymentStatus == PaymentStatuses.None)
                {
                    _logger.LogWarning("Received webhook for reservation {ReservationGuid} without initiated payment.", dto.ReservationGuid);
                    throw new InvalidOperationException("Payment not initiated.");
                }

                if (record.PaymentStatus == PaymentStatuses.Paid)
                {
                 //   return;
                }

                var isPaid = string.Equals(dto.Status, "PAID", StringComparison.OrdinalIgnoreCase);
                record.PaymentStatus = isPaid ? PaymentStatuses.Paid : PaymentStatuses.Failed;
                record.IdoStatus = isPaid ? ReservationStatusType.Accepted : record.IdoStatus;

                try
                {
                    await _store.UpdateAsync(record);

                    await _idoApi.FetchReservationByIDFromIdoSellAsync(record.IdoReservationId.Value, true,record.ReservationGuid.ToString("D"));

                    if (isPaid)
                    {
                        await EnsureIdoPaymentAsync(record.ReservationGuid);
                        //record = await EnsureBitrixContactAndDealAsync(record);
                        await GetDealEmailStatusAsync(record.ReservationGuid);
                        await CreatePaidUpsellOrderAsync(record, dto.ProviderTransactionId);
                    }
                    
                     await UpdateBitrixDealAsync(record, "API HandleTpayWebhookAsync - Payment status updated");
                    
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrency conflict while handling webhook for {ReservationGuid}. Retrying.", dto.ReservationGuid);
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }
            }
        }

        private async Task ConfirmIdoPaymentAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            await _idoApi.ConfirmPaymentsAsync([paymentId], cancellationToken);
        }



        //metoda tylko dla RentoomBooking (Nie Staywell, ):
        //po potwierdzeniu płatności za rezerwację wywołuję CreatePaidOrderAsync
        //z danymi  wierszy uzyskanymi z wybranych ofert dodatkowych rezerwacji (z rekordu rezerwacji) ,
        //aby upselle zakupione w ramach rezerwacji zalogować w tej samej tabeli wierszy co zakupy w StayWell!
        public async Task CreatePaidUpsellOrderAsync(ReservationRecord record, string providerTransactionId)
        {
            var request = record.State.StartRequest;
            if (request is null || request.SelectedUpsells.Count == 0)
            {
                return;
            }

            var culture = CultureInfo.CurrentUICulture.Name;
            var tiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(request.ObjectId, culture, "rentoombooking"); //<<--tylko dla rentoombooking!
            var tileLookup = tiles.ToDictionary(tile => tile.PartnerServiceId);

            var pricingContext = new ReservationPricingContext
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Adults = request.Adults,
                Children = request.Children,
                Currency = request.Currency ?? "PLN"
            };

            var lines = new List<UpsellOrderLineRecord>();
            foreach (var selected in request.SelectedUpsells)
            {
                if (!tileLookup.TryGetValue(selected.PartnerServiceId, out var tile))
                {
                    continue;
                }

                var quantity = Math.Max(1, selected.Quantity);
                var lineTotal = UpsellPricingCalculator.CalculateTotal(
                    tile.PricingModel,
                    tile.Price,
                    pricingContext.Nights,
                    pricingContext.TotalGuests,
                    quantity);

                lines.Add(new UpsellOrderLineRecord
                {
                    UpsellOrderGuid = Guid.Empty,
                    PartnerServiceId = tile.PartnerServiceId,
                    TitleSnapshot = tile.Title,
                    PricingModel = tile.PricingModel,
                    Quantity = quantity,
                    UnitPriceGross = tile.Price,
                    Nights = pricingContext.Nights,
                    TotalGuests = pricingContext.TotalGuests,
                    LineTotalGross = lineTotal,
                    Currency = request.Currency ?? "PLN",
                    LineStatus = UpsellLineStatuses.Paid,
                    UpsellDefinitionSnapshot = tile
                });
            }

            if (lines.Count == 0)
            {
                return;
            }
            //pełny order
            var orderRequest = new UpsellOrderRequest
            {
                ReservationGuid = record.ReservationGuid,
                ApartmentId = request.ObjectId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Adults = request.Adults,
                Children = request.Children,
                Currency = request.Currency ?? "PLN",
                Buyer = new UpsellBuyerDto
                {
                    Email = record.State.Client?.Email ?? string.Empty,
                    Name = string.Join(" ", new[] { record.State.Client?.FirstName, record.State.Client?.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
                },
                SelectedUpsells = request.SelectedUpsells //<<-- one też trafiają do osobnej tabeli gdzie mozna dowolnie je skanselowac, odwołać . Tu  jako JSON snapshot początkowy - taki audit log pierwotnego zakupu
                    .Select(u => new UpsellOrderLineRequest { PartnerServiceId = u.PartnerServiceId, Quantity = u.Quantity })
                    .ToList()
            };

            await _upsellOrderWorkflowService.CreatePaidOrderAsync(
                orderRequest,
                lines,
                providerTransactionId,
                record.Provider,
                DateTime.UtcNow);
        }

        public async Task<ReservationRecord> RequireReservationAsync(Guid reservationGuid, CancellationToken cancellationToken = default)
        {
            var record = await _store.GetAsync(reservationGuid, cancellationToken);
            return record ?? throw new InvalidOperationException($"Reservation {reservationGuid} not found.");
        }

        private async Task<PaymentDetails?> GetExistingIdoPaymentByTransactionIdAsync(ReservationRecord record, string transactionId, CancellationToken cancellationToken)
        {
            if (record.IdoReservationId is null)
            {
                return null;
            }

            var paymentsResponse = await _idoApi.GetPaymentsAsync(
                new PaymentGetParams
                {
                    ReservationIds = new List<int> { record.IdoReservationId.Value }
                },
                cancellationToken: cancellationToken);

            return paymentsResponse?.Results?
                .Where(payment => payment.ReservationId == record.IdoReservationId.Value)
                .FirstOrDefault(payment => string.Equals(payment.ExternalPaymentId, transactionId, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<RentoomReservation?> FetchReservationDocumentForRecordAsync(ReservationRecord record, Guid reservationGuid, CancellationToken cancellationToken)
        {
            if (!record.IdoReservationId.HasValue)
            {
                _logger.LogWarning("Reservation record {ReservationGuid} does not have IdoReservationId yet.", reservationGuid);
                return null;
            }

            var canonicalToken = reservationGuid.ToString("D");
            var fetchedReservation = await _idoApi.FetchReservationByIDFromIdoSellAsync(
                record.IdoReservationId.Value,
                saveToDb: true,
                existingResToken: canonicalToken,
                cancellationToken: cancellationToken);

            var reservation = fetchedReservation.ReservationResponse?.result?.Reservations?.FirstOrDefault();
            if (reservation is null)
            {
                _logger.LogWarning("IdoBooking returned no reservation for reservation {ReservationGuid} and IdoReservationId {IdoReservationId}.", reservationGuid, record.IdoReservationId);
                return null;
            }

            return new RentoomReservation
            {
                Id = reservation.id,
                ResToken = string.IsNullOrWhiteSpace(fetchedReservation.resToken) ? canonicalToken : fetchedReservation.resToken,
                Reservation = reservation
            };
        }

        public async Task<Reservation?> FetchIdoReservationAsync(ReservationRecord record, bool refreshCache, CancellationToken cancellationToken)
        {
            if (record.IdoReservationId is null)
            {
                return null;
            }

            var response = await _idoApi.FetchReservationByIDFromIdoSellAsync(
                record.IdoReservationId.Value,
                refreshCache,
                refreshCache ? record.ReservationGuid.ToString("D") : null,
                cancellationToken);

            return response?.ReservationResponse?.result?.Reservations?.FirstOrDefault();
        }

        private static IReadOnlyList<string> BuildReservationTokenCandidates(string reservationToken)
        {
            if (!Guid.TryParse(reservationToken, out var reservationGuid))
            {
                return new[] { reservationToken };
            }

            return new[]
            {
                reservationGuid.ToString("D"),
                reservationGuid.ToString("N"),
                reservationToken
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        }

        private async Task<ReservationRecord> EnsureIdoReservationAsync(ReservationRecord record, string initialStatus)
        {
            while (true)
            {
                if (record.IdoReservationId is not null)
                {
                    return record;
                }
                var apartmentInfo = ResolveApartmentInfo(record.State.StartRequest);
                var apartmentItemLocalSettings = await ResolveApartmentItemLocalSettingsAsync(record.State.StartRequest);
                UpdateReservationLocationState(record, apartmentInfo, apartmentItemLocalSettings);
                record.State.StayWellLink = BuildStayWellLink(record.ReservationGuid.ToString());
                var request = BuildReservationAddRequest(record, initialStatus);
                try
                {
                    var idoresponse = await _idoApi.AddReservationAsync(request);
                    
                    if (idoresponse?.Errors is not null )
                        throw new InvalidOperationException($"Reservation {record.ReservationGuid} couldn't be saved in Idobooking with error: {JsonConvert.SerializeObject(idoresponse.Errors)}.");

                    if (idoresponse.Reservations is not null && idoresponse.Reservations.Count > 0)
                    {
                        var resAddResult = idoresponse.Reservations[0];
                        
                        if (resAddResult.Error is not null)
                            throw new InvalidOperationException($"Failed to create IdoBooking reservation: {resAddResult.Error.FaultString}");

                        record.IdoReservationId = idoresponse.Reservations[0].ReservationId;
                        record.IdoStatus = initialStatus;

                        await _store.UpdateAsync(record);
                    }
                  //  record = await EnsureBitrixContactAndDealAsync(record);
                    return record;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning("Concurrency conflict while creating IdoBooking reservation for {ReservationGuid}. Retrying.", record.ReservationGuid);
                    record = await RequireReservationAsync(record.ReservationGuid);
                }
            }
        }



        private async Task UpdateIdoStatusAsync(ReservationRecord record, string targetStatus)
        {
            if (record.IdoReservationId is null)
            {
                return;
            }

            var request = new EditReservationsStatusRequest
                                                            {
                                                                ReservationId = record.IdoReservationId.Value,
                                                                Status = targetStatus,
                                                                Notify = ReservationNotifyType.No,
                                                                NotifyService = ReservationNotifyType.No
                                                            };

            
            await _idoApi.ChangeReservationStatusAsync(request);
        }


        private static NewReservation BuildReservationAddRequest(ReservationRecord record, string initialStatus)
        {
            if (record.State.StartRequest is null)
            {
                throw new InvalidOperationException("Reservation start request is missing.");
            }

            var start = record.State.StartRequest;
            var reservationAddons = (start.SelectedAddons ?? [])
                .Concat(start.MandatoryAddons ?? [])
                .GroupBy(addon => addon.AddonId)
                .Select(group => group.First())
                .ToList();



            var reservation = new NewReservation
            {
                RentoomResrvationID = record.ReservationGuid, //19.03.26  - działa, mozna zostawić. TODO 7.02.26: sprawdzic czy bedzie dzialac do zapisu idobooking -czy pominie to pole.
                DateFrom = start.StartDate.ToString("yyyy-MM-dd") + " " + start.CheckInTime.ToString("HH:mm"),
                DateTo = start.EndDate.ToString("yyyy-MM-dd") + " " + start.CheckOutTime.ToString("HH:mm"),
                Price = (float)start.getFullReservationPrizeWithoutUpsells(),//(float)start.OfferPrice.Value + (float)start.SelectedAddonsTotalPrice,  //19.03.26 - nie brało pod uwagę, zmieniłem. TODO: 10.03.26 - sprawdzić czy ta cena powinna iść do IDB i czy powinna zawierać ceny za Addony
                Status = initialStatus,
                InternalSource = ReservationInternalSourceType.Other,
                InternalNote = BuildInternalNoteForReservation(start),
                ApiNote = BuildInternalNoteForReservation(start),
                ExternalNote = record.State.StayWellLink,
                
                Items =
                [
                    new NewReservationItem
                {
                    ObjectItemId = start.ObjectItemId,
                    NumberOfAdults = start.Adults,
                    //NumberOfBigChildren = start.Children,
                    NumberOfSmallChildren = start.Children,
                    Addons = reservationAddons.Select(a => new NewReservationAddon
                    {
                        AddonId = a.AddonId,
                        Persons = a.Persons,
                        Nights = a.Nights,
                        Quantity = a.Quantity,
                        Price = a.Price,
                        Vat = a.Vat,
                    }).ToList()
                }
                ],
                Currency = start.Currency ?? "PLN",
                ClientData = MapClient(record.State.Client, record.State.Invoice)
            };

            /* return new ReservationAddRequest
             {
                 Params = new ReservationAddParams
                 {
                     Reservations = new List<NewReservation> { reservation }
                 }
             };*/
            return reservation;
        }

        private static string BuildInternalNoteForReservation(StartReservationRequest start)
        {
            return $"Typ_Oferty: {start.SelectedOfferType};\n" +
                   $"Kod_Rabatowy: {(start.AppliedBonusId.HasValue ? $"{start.AppliedBonusName} ({start.AppliedBonusValue}{(start.AppliedBonusValueType == BonusDiscountValueType.Percent ? "%" : "PLN")})" : "None")};\n" +
                   $"Bonus_Base_PLN: {start.BonusBasePln};\n" +
                   $"Discount_Amount_PLN: {start.DiscountAmountPln};\n";
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

        private string? BuildPaymentRetryLink(Guid reservationGuid, Guid? paymentSessionGuid, string? reservationSource, bool cancelaction = true)
        {
           if (reservationSource != null && !reservationSource.Contains("api", StringComparison.CurrentCultureIgnoreCase))
            {
                return string.Empty;
            }

            var baseUrl =
                Environment.GetEnvironmentVariable("Tpay__RetryPaymentRentoomSiteBaseUrl") ??
                Environment.GetEnvironmentVariable("Tpay:RetryPaymentRentoomSiteBaseUrl") ??
                Environment.GetEnvironmentVariable("RetryPaymentRentoomSiteBaseUrl") ??
                _configuration["Tpay:RetryPaymentRentoomSiteBaseUrl"] ??
                _configuration["RetryPaymentRentoomSiteBaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }
            string cancelactionquery = string.Empty;
            if (cancelaction)
                cancelactionquery = "&enableaction=cancel";

            return $"{baseUrl.TrimEnd('/')}/rezerwuj/{reservationGuid:D}/podsumowanie?payment_session={paymentSessionGuid:D}{cancelactionquery}";
        }


        private static ClientWithGuest? MapClient(ClientInfoDto? client, InvoiceInfoDto? invoice)
        {
            if (client is null) return null;
            var language = NormalizeIdoLanguage(client.Language);

            var guests = new List<ClientGuest>
        {
            new()
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                City = client.City,
                CountryCode = client.CountryCode.ToLower(),
                Email = client.Email,
                Language = language,
                Phone = client.Phone,
                Street = client.Street,
                Zipcode = client.ZipCode,
                
            }
        };

            return new ClientWithGuest
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                Email = client.Email,
                Phone = client.Phone,
                Street = client.Street,
                Zipcode = client.ZipCode,
                City = client.City,
                CountryCode = client.CountryCode.ToLower(),
                Currency = "PLN",
                Language = language,
                Guests = guests,
                CompanyName = invoice?.CompanyName,
                TaxNumber = invoice?.TaxNumber,
                /*idobooking wywala błąd gdy chcemy przekazac invoice data - po rozmowie z Krystianem zostawiamy to pole puste, a dane do faktury i tak trafiają do Bitrix w Dealu */
                /*  InvoiceData = invoice is null
                      ? null
                      : new ClientInvoiceData
                      {
                          FirstName = client.FirstName,
                          LastName = client.LastName,
                          CompanyName = invoice.CompanyName,
                          TaxNumber = invoice.TaxNumber,
                          Street = invoice.Street,
                          Zipcode = invoice.ZipCode,
                          City = invoice.City,
                          CountryCode = client.CountryCode
                      }*/
            };
        }

        private static string NormalizeIdoLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return "pol";
            }

            var normalized = language.Trim().ToLowerInvariant();
            if (!normalized.StartsWith("pl") && !normalized.StartsWith("pol"))
            {
                return "eng";
            }

            if (normalized.StartsWith("pl") || normalized.StartsWith("pol"))
            {
                return "pol";
            }

            if (normalized is "eng" or "pol")
            {
                return normalized;
            }

            return "pol";
        }

        private static string? ResolveBitrixLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            var normalized = language.Trim().ToLowerInvariant();
            if (normalized.StartsWith("en"))
            {
                return "eng";
            }

            if (normalized.StartsWith("pl"))
            {
                return "pol";
            }

            return normalized;
        }

        private static string ResolveAddonDetailsLanguage(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return "PL";
            }

            return cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "EN" : "PL";
        }

        private async Task<string> BuildPurchasedAddonsBitrixValueAsync(StartReservationRequest? startRequest)
        {
            var selectedAddons = startRequest?.SelectedAddons?
                .Where(addon => addon.AddonId > 0)
                .ToList();

            if (selectedAddons is null || selectedAddons.Count == 0)
            {
                return string.Empty;
            }

            var definedAddons = await _apartmentStore.GetDefinedAddonsAsync();
            var addonNameLookup = definedAddons.ToDictionary(
                addon => addon.IdoBookingId,
                addon => addon.AddonDefinition?.Details?
                             .FirstOrDefault(detail => string.Equals(detail.Lang, "PL", StringComparison.OrdinalIgnoreCase))?.Name
                         ?? addon.Name);

            var addonNames = selectedAddons
                .GroupBy(addon => addon.AddonId)
                .Select(group =>
                {
                    var addonName = addonNameLookup.GetValueOrDefault(group.Key)?.Trim();
                    if (string.IsNullOrWhiteSpace(addonName))
                    {
                        return null;
                    }

                    var totalQuantity = group.Sum(addon => addon.Quantity > 0 ? addon.Quantity : 1);
                    return $"{addonName} x {totalQuantity}";
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            return addonNames.Count == 0
                ? string.Empty
                : string.Join(", ", addonNames);
        }

        private static string? BuildCompanyAddress(InvoiceInfoDto? invoice)
        {
            if (invoice is null)
            {
                return null;
            }

            var parts = new[]
            {
                invoice.City?.Trim(),
                invoice.ZipCode?.Trim(),
                invoice.Street?.Trim()
            }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

            return parts.Count == 0 ? null : string.Join(", ", parts);
        }


        public async Task<ReservationRecord> EnsureBitrixContactAndDealAsync(ReservationRecord record)
        {
            if (record.State.Client is null)
            {
                return record;
            }

            var invoice = record.State.Invoice;

            var contactRequest = new CreateContactRequest
            {
                FirstName = record.State.Client.FirstName,
                LastName = record.State.Client.LastName,
                Email = record.State.Client.Email,
                Phone = record.State.Client.Phone,
                ReservationId = record.IdoReservationId,
                AssignedById = _bitrixAssignedByUserId,
                TaxNumber = invoice?.TaxNumber,
                CompanyName = invoice?.CompanyName,
                CompanyEmail = invoice?.Email,
                CompanyAddress = BuildCompanyAddress(invoice)
            };

            var updated = false;

            if (!record.ClientBitrixId.HasValue)
            {
                record.ClientBitrixId = await _bitrixService.UpsertContactByEmailAsync(contactRequest);
                updated = true;
                _logger.LogInformation("Upserted Bitrix contact {ContactId} for reservation {ReservationGuid}.", record.ClientBitrixId, record.ReservationGuid);
            }
            else
            {
                await _bitrixService.UpdateContactAsync(record.ClientBitrixId.Value, contactRequest);
                updated = true;
                _logger.LogInformation("Updated Bitrix contact {ContactId} for reservation {ReservationGuid}.", record.ClientBitrixId, record.ReservationGuid);
            }
            await EnsurePaymentTotalsAsync(record.ReservationGuid,record);
            if (!record.DealBitrixId.HasValue)
            {
               

                var pipelines = await _bitrixService.GetDealPipelinesAsync();
                var pipelineName = BitrixConfiguration.GetReservationPipelineName(_configuration);
                var rentalPipeline = pipelines.FirstOrDefault(p => string.Equals(p.Name, pipelineName, StringComparison.OrdinalIgnoreCase));
                var pipelineId = rentalPipeline?.Id ?? 0;
                var stages = await _bitrixService.GetDealStagesAsync(pipelineId);
                var newStage = stages.FirstOrDefault(s => string.Equals(s.Name, "W toku", StringComparison.OrdinalIgnoreCase));

                var dealTitle = record.IdoReservationId.HasValue
                    ? $"Rezerwacja #{record.IdoReservationId}"
                    : $"Rezerwacja {record.ReservationGuid:D}";
                var apartmentInfo = ResolveApartmentInfo(record.State.StartRequest);
                var apartmentItemLocalSettings = await ResolveApartmentItemLocalSettingsAsync(record.State.StartRequest);
                var purchasedAddonsValue = await BuildPurchasedAddonsBitrixValueAsync(record.State.StartRequest);
                var reservationSourceValue = await ResolveBitrixReservationSourceValueAsync(record);
                UpdateReservationLocationState(record, apartmentInfo, apartmentItemLocalSettings);
                var startRequest = record.State.StartRequest;
                if (startRequest is null)
                {
                    throw new ArgumentNullException(nameof(record.State.StartRequest), "Bitrix reservation sync requires a start request.");
                }

                var reservationStartOffset = GetWarsawOffset(startRequest.StartDate, startRequest.CheckInTime);
                var bitrixServerUTCOffset = await _bitrixService.GetServerUtcOffsetAsync();
                var differenceInHours = bitrixServerUTCOffset.TotalHours - reservationStartOffset.TotalHours;
                var customFields = new Dictionary<string, object?>
                {
                    ["UF_CRM_1773079785969"] = record.State.Invoice is not null,
                    ["UF_CRM_1769797476812"] = ResolveBitrixLanguage(record.State.Client.Language),
                    ["UF_CRM_1769797498979"] = record.State.Client.CountryCode,
                    ["UF_CRM_1768836801823"] = startRequest?.Adults,
                    ["UF_CRM_1768836818927"] = startRequest?.EndDate.DayNumber - startRequest?.StartDate.DayNumber,
                    ["UF_CRM_1773256016575"] = ToBitrixDateTime(startRequest?.StartDate, startRequest?.CheckInTime, bitrixServerUTCOffset, differenceInHours),
                    ["UF_CRM_1773310028374"] = ToBitrixDateTime(startRequest?.EndDate, startRequest?.CheckOutTime, bitrixServerUTCOffset, differenceInHours),
                    
                    //RB_Godzina_Zameldowania
                    ["UF_CRM_1778170129465"] = startRequest?.CheckInTime.ToString("HH:mm"),
                    //RB_Godzina_Wymeldowania
                    ["UF_CRM_1778170154231"] = startRequest?.CheckOutTime.ToString("HH:mm"),

                    ["UF_CRM_1773310079975"] = startRequest?.CheckInTime < new TimeOnly(15, 0),
                    ["UF_CRM_1773310094605"] = startRequest?.CheckOutTime > new TimeOnly(11, 0),
                    [BitrixPurchasedAddonsFieldName] = purchasedAddonsValue,
                    [BitrixReservationSourceFieldName] = reservationSourceValue,
                    [BitrixService.IdoReservationIdFieldName] = record.IdoReservationId,
                    [BitrixStayWellLinkFieldName] = BuildStayWellLink(record.ReservationGuid.ToString()),
                    //RB_Link_Anuluj_Rezerwacje
                    ["UF_CRM_1775071948450"] = BuildPaymentRetryLink(record.ReservationGuid, record.PaymentSessionGuid, reservationSourceValue,cancelaction: true),
                    //RB_Link_Do_Platnosci
                    ["UF_CRM_1775071642554"] = BuildPaymentRetryLink(record.ReservationGuid, record.PaymentSessionGuid, reservationSourceValue, cancelaction: false)



                };
                AddBitrixLocationFields(customFields, apartmentInfo, apartmentItemLocalSettings);

                var dealUpdateFields = new Dictionary<string, object?>
                {
                    ["TITLE"] = dealTitle,
                    ["ASSIGNED_BY_ID"] = _bitrixAssignedByUserId
                };

                
                    
                

                if (record.State.PaymentGrandTotal > 0)
                {
                    dealUpdateFields["OPPORTUNITY"] = record.State.PaymentGrandTotal;
                }

                if (!string.IsNullOrWhiteSpace(record.State.StartRequest?.Currency))
                {
                    dealUpdateFields["CURRENCY_ID"] = record.State.StartRequest.Currency;
                }

                if (record.ClientBitrixId.HasValue)
                {
                    dealUpdateFields["CONTACT_ID"] = record.ClientBitrixId.Value;
                }

                foreach (var customField in customFields)
                {
                    dealUpdateFields[customField.Key] = customField.Value;
                }

                dealUpdateFields["COMMENT"] = DateTime.UtcNow.ToString() +  ": EnsureBitrixContactAndDealAsync";

                record.DealBitrixId = await _bitrixService.UpsertDealAsync(record.DealBitrixId, new CreateDealRequest(
                    Title: dealTitle,
                    CategoryId: pipelineId,
                    StageId: newStage?.StageId ?? "NEW",
                    AssignedById: _bitrixAssignedByUserId,
                    Opportunity: record.State.PaymentGrandTotal, //record.State.StartRequest?.OfferPrice,
                    CurrencyId: record.State.StartRequest?.Currency ?? "PLN",
                    ContactId: record.ClientBitrixId,
                    CustomFields: customFields
                ), dealUpdateFields);

                updated = true;
                _logger.LogInformation("Upserted Bitrix deal {DealId} for reservation {ReservationGuid}.", record.DealBitrixId, record.ReservationGuid);
            }

            if (updated)
            {
                await _store.UpdateAsync(record);
            }

            return record;
        }

        public async Task UpdateBitrixDealAsync(ReservationRecord record, string updateReason, Reservation? idoReservation = null)
        {
            if (!record.DealBitrixId.HasValue)
            {
                return;
            }

            if (record.IdoReservationId.HasValue)
            {
                idoReservation ??= await FetchIdoReservationAsync(record, refreshCache: false, CancellationToken.None);
            }
            var apartmentInf = ResolveApartmentInfo(record.State.StartRequest, idoReservation);
            var apartmentItemLocalSettings = await ResolveApartmentItemLocalSettingsAsync(record.State.StartRequest, idoReservation);
            var purchasedAddonsValue = await BuildPurchasedAddonsBitrixValueAsync(record.State.StartRequest);
            var reservationSourceValue = await ResolveBitrixReservationSourceValueAsync(record, idoReservation);
            var stateLocationChanged = UpdateReservationLocationState(record, apartmentInf, apartmentItemLocalSettings);

            //pola UF_CRM* to pola customowe - tu sa wpisane na sztywno ale mozna je pobrac z bitrixa dynamicznie jesli trzeba.. ewentualne TODO.
            var fields = new Dictionary<string, object?>
            {
                ["COMMENTS"] = $"{DateTime.Now.ToString()}: Status Rezerwacji {record.IdoReservationId} (z IDB): {record.IdoStatus ?? "Unknown"}, Status Platnosci TPAY: {record.PaymentStatus} ({updateReason}).",
                //RB_Status_Platnosci
                ["UF_CRM_1768566732609"] = record.PaymentStatus,
                //Czy faktura
                ["UF_CRM_1773079785969"] = record.State.Invoice is not null,
                //Jezyk
                ["UF_CRM_1769797476812"] = ResolveBitrixLanguage(record.State.Client?.Language),
                //RB_Status_Rezerwacji
                ["UF_CRM_1768566710921"] = record.IdoStatus,
                //RB_KodTpay_Platnosci
                ["UF_CRM_1768566766553"] = string.Empty,
                [BitrixPurchasedAddonsFieldName] = purchasedAddonsValue,
                [BitrixReservationSourceFieldName] = reservationSourceValue,
                //RB_ID_Rezrerwacji
                [BitrixService.IdoReservationIdFieldName] = record.IdoReservationId,
                //RB_Link_StayWell
                [BitrixStayWellLinkFieldName] = BuildStayWellLink(record.ReservationGuid.ToString()),

                //RB_Godzina_Zameldowania
                ["UF_CRM_1778170129465"] = record.State.StartRequest?.CheckInTime.ToString("HH:mm"),
                //RB_Godzina_Wymeldowania
                ["UF_CRM_1778170154231"] = record.State.StartRequest?.CheckOutTime.ToString("HH:mm"),

            };
            AddBitrixLocationFields(fields, apartmentInf, apartmentItemLocalSettings);

            //RB_Link_Anuluj_Rezerwacje
            fields["UF_CRM_1775071948450"] = BuildPaymentRetryLink(record.ReservationGuid, record.PaymentSessionGuid, reservationSourceValue, cancelaction: true);

            //RB_Link_Do_Platnosci
            fields["UF_CRM_1775071642554"] = BuildPaymentRetryLink(record.ReservationGuid, record.PaymentSessionGuid, reservationSourceValue, cancelaction: false);

            if (record.State.PaymentGrandTotal >0)
            {
                fields["OPPORTUNITY"] = record.State.PaymentGrandTotal; //record.State.StartRequest.OfferPrice.Value;
            }

            if (!string.IsNullOrWhiteSpace(record.State.StartRequest?.Currency))
            {
                fields["CURRENCY_ID"] = record.State.StartRequest.Currency;
            }

            if (record.ClientBitrixId.HasValue)
            {
                fields["CONTACT_ID"] = record.ClientBitrixId.Value;
            }

            var apartmentName = idoReservation?.Items?.FirstOrDefault()?.objectName;
            if (!string.IsNullOrWhiteSpace(apartmentName))
            {
                //RB_Nazwa_Apartamentu
                fields["UF_CRM_1768566682522"] = apartmentName;
            }

            var location = apartmentInf?.ObjectLocation?.LocalizationItem;
            if (location is not null)
            {
                //RB_Adres_Apartamentu
                fields["UF_CRM_1768840472108"] = $"{location.ZipCode} {location.City}, ul. {location.Street}";
            }

            if (idoReservation?.ReservationDetails is not null)
            {
                //RB_Poczatek_Rezerwacji
                fields["UF_CRM_1768566963962"] = idoReservation.ReservationDetails.dateFrom;
                fields["BEGINDATE"] = idoReservation.ReservationDetails.dateFrom; //deal field

                //RB_Koniec_Rezerwacji
                fields["UF_CRM_1768566980297"] = idoReservation.ReservationDetails.dateTo;
                fields["CLOSEDATE"] = idoReservation.ReservationDetails.dateTo; //deal field

                //RB_Ilosc_Nocy
                fields["UF_CRM_1768836818927"] = idoReservation.ReservationDetails.getDuration();
            }

            if (idoReservation?.Client?.Guests is not null)
            {
                //RB_Ilosc_Gosci
                fields["UF_CRM_1768836801823"] = idoReservation.Items[0].numberOfAdults;
            }

            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, fields);

            if (stateLocationChanged)
            {
                await _store.UpdateAsync(record);
            }
        }

      

        public async Task<DealEmailStatusDto> GetDealEmailStatusAsync(Guid reservationGuid)
        {
            var record = await RequireReservationAsync(reservationGuid);

            if (!record.DealBitrixId.HasValue)
            {
                record = await EnsureBitrixContactAndDealAsync(record);
            }

            if (!record.DealBitrixId.HasValue)
            {
            
                
                
                return new DealEmailStatusDto
                {
                    EmailSent = false,
                    HasActivities = false
                };
            }

            //najpierw sprawdz czy w bazie jest.
            if (!String.IsNullOrEmpty(record.DealBitrixSentConfirmationEmailId))
            {
                return new DealEmailStatusDto
                {
                    EmailSent = true,
                    HasActivities = true
                };
            }

            var activities = await _bitrixService.ListDealEmailActivitiesAsync(record.DealBitrixId.Value);
            
            var latest = activities.FirstOrDefault();
            var emailSent = latest is not null
                && string.Equals(latest.Completed, "Y", StringComparison.OrdinalIgnoreCase)
                && string.Equals(latest.Status, "2", StringComparison.OrdinalIgnoreCase);
            record.DealBitrixSentConfirmationEmailId = emailSent ? latest.Id : null;
            
           await _store.UpdateAsync(record); 

            return new DealEmailStatusDto
            {
                EmailSent = emailSent, // czy byl wyslany mail z bitrixa
                HasActivities = activities.Count > 0, // czy w ogole sa maile w pipeline
                LatestActivity = latest,
                Activities = activities
            };
        }

        public async Task SaveCustomerTermsAsync(Guid reservationGuid, Dictionary<int, bool> termSelections)
        {
            var record = await RequireReservationAsync(reservationGuid);
            record = await EnsureBitrixContactAndDealAsync(record);
            var agreedEntities = termSelections.Select(kvp => new CustomerAgreedTerms
            {
                ReservationGuid = reservationGuid,
                TermsSourceId = kvp.Key,
                IsAccepted = kvp.Value, 
                AgreedAt = DateTime.UtcNow,
                ClientBitrixId = record.ClientBitrixId,
            }).ToList();

            await _termsRepository.SaveAgreedTermsAsync(agreedEntities);
            var agreedTermsDetails = await _termsRepository.GetAgreedTermsByReservationAsync(reservationGuid);

            await _bitrixService.UpdateContactAdditionalTermsAsync(record.ClientBitrixId.Value, agreedTermsDetails);

            _logger.LogInformation("Saved {Count} term states for reservation {Guid}", agreedEntities.Count, reservationGuid);
        }

        //<summary>
        /// Metoda do ręcznego oznaczania płatności jako opłaconej. Używana tylko przy lokalnym ustawieniu _idoApi.UseDummyIdoBooking = true, czyli przy testach bez integracji z Idobooking. Oznacza rezerwację jako opłaconą zarówno w naszej bazie, jak i w Bitrixie, ale nie dodaje płatności do Idobooking ani nie zmienia statusu rezerwacji w Idobooking (bo w trybie dummy nie tworzymy rezerwacji w Idobooking).
        /// W trybie dummy też nie dostajemy webhooków z TPAY (gdy na lokalnej maszynie), więc ta metoda pozwala zasymulować sytuację po opłaceniu rezerwacji, żeby móc przetestować dalsze kroki workflow, takie jak wysyłka maili z Bitrixa czy zmiana statusu rezerwacji.
        //</summary>
        public async Task MarkPaymentAsPaidAsync(Guid reservationGuid)
        {
            if (!_idoApi.UseDummyIdoBooking)
            {
                return;
            }

            var record = await RequireReservationAsync(reservationGuid);
            if (record.PaymentStatus == PaymentStatuses.Paid)
            {
                return;
            }

            record.PaymentStatus = PaymentStatuses.Paid;
            record.IdoStatus = ReservationStatusType.Accepted;
            await _store.UpdateAsync(record);

            var dto = new TpayWebhookDto
            {
                ReservationGuid = record.ReservationGuid,
                PaymentSessionGuid = record.PaymentSessionGuid.Value,
                ProviderTransactionId = record.ProviderTransactionId,
                Status = PaymentStatuses.Paid,
                Signature = "validated"
            };

            await HandleTpayWebhookAsync(dto);

            
            var fields = new Dictionary<string, object?>()
            {
                //RB_Status_Rezerwacji
                ["UF_CRM_1768566710921"] = ReservationStatusType.Accepted,
                //RB_Status_Platnosci
                ["UF_CRM_1768566732609"] = record.PaymentStatus

            };
            await _bitrixService.UpdateDealAsync(record.DealBitrixId.Value, fields);
            
        }
    }
}
