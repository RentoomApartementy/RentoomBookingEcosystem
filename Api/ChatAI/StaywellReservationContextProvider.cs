using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Logging;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Services;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.PartnersAndServices.Enums;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;

namespace RentoomBooking.Api.Chat;

public sealed class StaywellReservationContextProvider : IReservationContextProvider
{
    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private readonly IReservationStore _reservationStore;
    private readonly ApartmentRepository _apartmentRepository;
    private readonly RappQrMaintService _qrMaintService;
    private readonly ArrivalInstructionsService _arrivalInstructionsService;
    private readonly LockInstructionsService _lockInstructionsService;
    private readonly CustomerTermsRepository _customerTermsRepository;
    private readonly ILogger<StaywellReservationContextProvider> _logger;
    private readonly IdoSellService _idosellService;
    private readonly IUpsellCatalogService _upsellCatalogService;
    private readonly IUpsellOrderStore _upsellOrderStore;

    public StaywellReservationContextProvider(
        IReservationStore reservationStore,
        ApartmentRepository apartmentRepository,
        RappQrMaintService qrMaintService,
        ArrivalInstructionsService arrivalInstructionsService,
        LockInstructionsService lockInstructionsService,
        CustomerTermsRepository customerTermsRepository,
        IdoSellService idosellService,
        IUpsellCatalogService upsellCatalogService,
        IUpsellOrderStore upsellOrderStore,
        ILogger<StaywellReservationContextProvider> logger)
    {
        _reservationStore = reservationStore;
        _apartmentRepository = apartmentRepository;
        _qrMaintService = qrMaintService;
        _arrivalInstructionsService = arrivalInstructionsService;
        _lockInstructionsService = lockInstructionsService;
        _customerTermsRepository = customerTermsRepository;
        _idosellService = idosellService;
        _upsellCatalogService = upsellCatalogService;
        _upsellOrderStore = upsellOrderStore;
        _logger = logger;
    }

    public async Task<ReservationPromptContext?> GetContextAsync(int reservationId, string reservationToken, CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
        {
            return null;
        }

        var rentoomReservation = await _idosellService.FetchReservationByIDFromIdoSellAsync(
            reservationId,
            saveToDb: false,
            existingResToken: reservationToken,
            cancellationToken: cancellationToken);

        var reservation = rentoomReservation?.ReservationResponse?.result?.Reservations?.FirstOrDefault();
        if (reservation is null)
        {
            return null;
        }

        var toDate = reservation.ReservationDetails?.getDateTo();
        if (toDate.HasValue && toDate.Value.Date < DateTime.UtcNow.Date)
        {
            return null;
        }

        var reservationItem = reservation.Items?.FirstOrDefault();
        var locale = NormalizeLanguage(reservation.Client?.Language);
        var upsellLocale = ResolveUpsellLocale(reservation.Client?.Language);
        var apartmentItemId = ResolveApartmentItemId(reservationItem);
        var reservationRecord = await ResolveReservationRecordAsync(reservationToken, reservation.id, cancellationToken);
        var reservationGuid = ResolveReservationGuid(reservationToken, reservationRecord);
        var apartmentInfo = ResolveApartmentInfo(reservationItem);
        var location = apartmentInfo?.ObjectLocation?.LocalizationItem;

        string? wifiSsid = null;
        string? wifiPassword = null;
        string? instructionsSummary = null;
        ApartmentItemLocalSettings? apartmentCodes = null;

        if (apartmentItemId > 0)
        {
            var wifi = await _qrMaintService.GetWifiInfoAsync(apartmentItemId, cancellationToken);
            wifiSsid = wifi?.Ssid;
            wifiPassword = wifi?.Password;

            apartmentCodes = await _qrMaintService.GetApartmentItemCodesAsync(apartmentItemId, cancellationToken);

            var instructions = await _arrivalInstructionsService
                .GetArrivalInstructionStepsAsync(apartmentItemId, locale, cancellationToken);

            instructionsSummary = BuildInstructionsSummary(instructions);
        }

        var rules = await _customerTermsRepository.GetActiveTermsSourcesAsync(locale, onlyRequiredForWorkflow: false);
        var rulesSummary = BuildRulesSummary(rules);
        var lockInstructionsSummary = BuildLockInstructionsSummary(_lockInstructionsService.GetLockInstructions(locale));
        var apartmentAddress = BuildApartmentAddress(apartmentInfo, location);
        var apartmentGoogleMapsUrl = BuildApartmentGoogleMapsUrl(
            reservationRecord?.State?.GoogleMapsLink,
            location?.GeoLocationLat,
            location?.GeoLocationLng);
        var parkingMapUrl = FirstNonEmpty(
            reservationRecord?.State?.ParkingMapUrl,
            apartmentCodes?.ParkingMapUrl);
        var apartmentDirectionsSummary = BuildApartmentDirectionsSummary(location);
        var receptionInfo = CleanToPlainText(location?.ReceptionInfo, 500);
        var parkingSpotNumber = NormalizeValue(apartmentCodes?.ParkingSpotNumber);
        var apartmentNumberOrItemCode = BuildApartmentNumberOrItemCode(reservationItem);
        var remoteOpenSupported = !string.IsNullOrWhiteSpace(apartmentCodes?.TTLockId);
        var parkingInfoSummary = BuildParkingInfoSummary(parkingSpotNumber, parkingMapUrl);
        var accessMethodSummary = BuildAccessMethodSummary(apartmentCodes, apartmentNumberOrItemCode, remoteOpenSupported);
        var apartmentLocationSummary = BuildApartmentLocationSummary(location, apartmentAddress, apartmentGoogleMapsUrl);
        var nearbyGuidance = BuildNearbyAnswerGuidance(location, apartmentAddress);
        var availableUpsellsSummary = await BuildAvailableUpsellsSummaryAsync(
            reservationGuid,
            apartmentItemId,
            upsellLocale,
            cancellationToken);

        return new ReservationPromptContext
        {
            ReservationToken = reservationToken,
            GuestName = BuildGuestName(reservation.Client?.FirstName, reservation.Client?.LastName),
            GuestEmail = reservation.Client?.Email,
            GuestPhone = reservation.Client?.Phone,
            ApartmentName = reservationItem?.objectName,
            ApartmentAddress = apartmentAddress,
            CheckInDate = reservation.ReservationDetails?.getDateFrom().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CheckOutDate = reservation.ReservationDetails?.getDateTo().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ReservationStatus = reservation.ReservationDetails?.status.ToString(),
            WifiSsid = wifiSsid,
            WifiPassword = wifiPassword,
            ArrivalInstructionsSummary = instructionsSummary,
            LockInstructionsSummary = lockInstructionsSummary,
            RulesSummary = rulesSummary,
            Locale = locale,
            ApartmentCity = NormalizeValue(location?.City),
            ApartmentRegion = NormalizeValue(location?.Region),
            ApartmentCountry = NormalizeValue(location?.Country),
            ApartmentGeoLatitude = location?.GeoLocationLat,
            ApartmentGeoLongitude = location?.GeoLocationLng,
            ApartmentGoogleMapsUrl = apartmentGoogleMapsUrl,
            ApartmentDirectionsSummary = apartmentDirectionsSummary,
            ReceptionInfo = receptionInfo,
            ApartmentLocationSummary = apartmentLocationSummary,
            ParkingSpotNumber = parkingSpotNumber,
            ParkingMapUrl = parkingMapUrl,
            ParkingInfoSummary = parkingInfoSummary,
            GateCode = NormalizeValue(apartmentCodes?.GateCode),
            BuildingCode = NormalizeValue(apartmentCodes?.GateDoorCode),
            AdditionalDoorCode = NormalizeValue(apartmentCodes?.AdditionalDoorCode),
            StoreroomCode = NormalizeValue(apartmentCodes?.StoreroomCode),
            ApartmentNumberOrItemCode = apartmentNumberOrItemCode,
            RemoteOpenSupported = remoteOpenSupported,
            AccessMethodSummary = accessMethodSummary,
            NearbyAnswerGuidance = nearbyGuidance,
            AvailableUpsellsSummary = availableUpsellsSummary
        };
    }

    private async Task<string?> BuildAvailableUpsellsSummaryAsync(
        Guid? reservationGuid,
        int apartmentItemId,
        string upsellLocale,
        CancellationToken cancellationToken)
    {
        if (!reservationGuid.HasValue || reservationGuid.Value == Guid.Empty || apartmentItemId <= 0)
        {
            return null;
        }

        try
        {
            var availableTiles = await _upsellCatalogService.GetUpsellTilesForApartmentAsync(
                apartmentItemId,
                upsellLocale,
                "staywell",
                cancellationToken);

            var orders = await _upsellOrderStore.GetByReservationGuidAsync(reservationGuid.Value, cancellationToken);

            var alreadyPurchasedServiceIds = orders
                .Where(order => string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .SelectMany(order => order.Lines)
                .Where(line => string.Equals(line.LineStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.PartnerServiceId)
                .ToHashSet();

            var onlyAvailable = availableTiles
                .Where(tile => !alreadyPurchasedServiceIds.Contains(tile.PartnerServiceId)
                            || tile.PricingModel == PartnerServicePricingModel.OneTime)
                .ToList();

            if (onlyAvailable.Count == 0)
            {
                return "No currently available upsells for this reservation.";
            }

            var lines = onlyAvailable
                .Take(30)
                .Select(tile =>
                {
                    var title = Trim(tile.Title, 120);
                    var description = Trim(CleanToPlainText(tile.ShortDescription, 180), 180);
                    var partner = Trim(tile.PartnerInfo?.Name, 80);
                    var partnerType = GetEnumText(tile.PartnerInfo?.PartnerType) ?? "unknown";
                    var price = tile.Price.ToString("0.##", CultureInfo.InvariantCulture);
                    var currency = string.IsNullOrWhiteSpace(tile.Currency) ? "PLN" : tile.Currency.Trim();

                    return $"- ServiceId: {tile.PartnerServiceId}; Title: {title}; Price: {price} {currency}; PricingModel: {tile.PricingModel}; Partner: {partner}; PartnerType: {partnerType}; Description: {description}";
                });

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load available upsells for reservationGuid={ReservationGuid}, apartmentItemId={ApartmentItemId}.",
                reservationGuid,
                apartmentItemId);
            return null;
        }
    }

    private static Guid? ResolveReservationGuid(string reservationToken, ReservationRecord? reservationRecord)
    {
        if (reservationRecord is not null && reservationRecord.ReservationGuid != Guid.Empty)
        {
            return reservationRecord.ReservationGuid;
        }

        if (Guid.TryParse(reservationToken, out var guid))
        {
            return guid;
        }

        return null;
    }

    private static string ResolveUpsellLocale(string? reservationLanguage)
    {
        return string.IsNullOrWhiteSpace(reservationLanguage)
            ? "pl"
            : reservationLanguage.Trim();
    }

    private static string? GetEnumText<TEnum>(TEnum? value) where TEnum : struct, Enum
    {
        if (!value.HasValue)
        {
            return null;
        }

        var enumValue = value.Value;
        var member = typeof(TEnum).GetMember(enumValue.ToString()).FirstOrDefault();
        if (member is null)
        {
            return enumValue.ToString();
        }

        var description = member.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        var display = member.GetCustomAttribute<DisplayAttribute>()?.GetName();
        if (!string.IsNullOrWhiteSpace(display))
        {
            return display.Trim();
        }

        return enumValue.ToString();
    }

    private async Task<ReservationRecord?> ResolveReservationRecordAsync(string reservationToken, int idoReservationId, CancellationToken cancellationToken)
    {
        ReservationRecord? record = null;

        if (Guid.TryParse(reservationToken, out var reservationGuid))
        {
            record = await _reservationStore.GetAsync(reservationGuid, cancellationToken);
        }

        if (record is null && idoReservationId > 0)
        {
            record = await _reservationStore.GetByIdoReservationIdAsync(idoReservationId, cancellationToken);
        }

        return record;
    }

    private ApartmentObject? ResolveApartmentInfo(ReservationItem? reservationItem)
    {
        if (reservationItem is null)
        {
            return null;
        }

        try
        {
            if (reservationItem.objectId > 0)
            {
                return _apartmentRepository.FindApartmentInPostgres(reservationItem.objectId);
            }

            if (reservationItem.objectItemId > 0)
            {
                return _apartmentRepository.FindApartmentByItemId(reservationItem.objectItemId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve apartment metadata for objectId={ObjectId}, objectItemId={ObjectItemId}.",
                reservationItem.objectId,
                reservationItem.objectItemId);
        }

        return null;
    }

    private static int ResolveApartmentItemId(ReservationItem? reservationItem)
    {
        if (reservationItem is null)
        {
            return 0;
        }

        if (reservationItem.objectItemId > 0)
        {
            return reservationItem.objectItemId;
        }

        if (reservationItem.itemId > 0)
        {
            return reservationItem.itemId;
        }

        return 0;
    }

    private static string? BuildApartmentAddress(ApartmentObject? apartmentInfo, LocalizationItem? location)
    {
        if (!string.IsNullOrWhiteSpace(apartmentInfo?.ObjectLocation?.Address))
        {
            return apartmentInfo.ObjectLocation.Address.Trim();
        }

        var parts = new[]
        {
            NormalizeValue(location?.Street),
            NormalizeValue(location?.ZipCode),
            NormalizeValue(location?.City),
            NormalizeValue(location?.Country)
        }
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToArray();

        if (parts.Length == 0)
        {
            return null;
        }

        return string.Join(", ", parts);
    }

    private static string? BuildApartmentGoogleMapsUrl(string? workflowGoogleMapsLink, float? latitude, float? longitude)
    {
        var workflowLink = NormalizeValue(workflowGoogleMapsLink);
        if (!string.IsNullOrWhiteSpace(workflowLink))
        {
            return workflowLink;
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return null;
        }

        var lat = latitude.Value.ToString(CultureInfo.InvariantCulture);
        var lng = longitude.Value.ToString(CultureInfo.InvariantCulture);
        return $"https://www.google.com/maps?q={lat},{lng}";
    }

    private static string? BuildApartmentDirectionsSummary(LocalizationItem? location)
    {
        var plain = CleanToPlainText(location?.DirectionsInfoPlainText, 1200);
        if (!string.IsNullOrWhiteSpace(plain))
        {
            return plain;
        }

        return CleanToPlainText(location?.DirectionsInfo, 1200);
    }

    private static string? BuildApartmentLocationSummary(LocalizationItem? location, string? apartmentAddress, string? apartmentGoogleMapsUrl)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(apartmentAddress))
        {
            parts.Add($"Address: {apartmentAddress}");
        }

        var area = string.Join(", ", new[]
        {
            NormalizeValue(location?.City),
            NormalizeValue(location?.Region),
            NormalizeValue(location?.Country)
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        if (!string.IsNullOrWhiteSpace(area))
        {
            parts.Add($"Area: {area}");
        }

        if (location?.GeoLocationLat is not null && location.GeoLocationLng is not null)
        {
            parts.Add($"GPS: {location.GeoLocationLat.Value.ToString(CultureInfo.InvariantCulture)}, {location.GeoLocationLng.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(apartmentGoogleMapsUrl))
        {
            parts.Add($"Map: {apartmentGoogleMapsUrl}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string? BuildParkingInfoSummary(string? parkingSpotNumber, string? parkingMapUrl)
    {
        if (string.IsNullOrWhiteSpace(parkingSpotNumber) && string.IsNullOrWhiteSpace(parkingMapUrl))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(parkingSpotNumber) && !string.IsNullOrWhiteSpace(parkingMapUrl))
        {
            return $"Parking spot: {parkingSpotNumber}. Parking map: {parkingMapUrl}";
        }

        if (!string.IsNullOrWhiteSpace(parkingSpotNumber))
        {
            return $"Parking spot: {parkingSpotNumber}";
        }

        return $"Parking map: {parkingMapUrl}";
    }

    private static string? BuildAccessMethodSummary(ApartmentItemLocalSettings? apartmentCodes, string? apartmentNumberOrItemCode, bool remoteOpenSupported)
    {
        var parts = new List<string>();

        if (remoteOpenSupported)
        {
            parts.Add("Remote open in StayWell may be available for this apartment.");
        }

        if (!string.IsNullOrWhiteSpace(apartmentCodes?.GateCode))
        {
            parts.Add($"Gate code: {apartmentCodes.GateCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(apartmentCodes?.GateDoorCode))
        {
            parts.Add($"Building code: {apartmentCodes.GateDoorCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(apartmentCodes?.AdditionalDoorCode))
        {
            parts.Add($"Additional door code: {apartmentCodes.AdditionalDoorCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(apartmentCodes?.StoreroomCode))
        {
            parts.Add($"Storeroom/keysafe code: {apartmentCodes.StoreroomCode.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(apartmentNumberOrItemCode))
        {
            parts.Add($"Apartment number/item code: {apartmentNumberOrItemCode}");
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string BuildNearbyAnswerGuidance(LocalizationItem? location, string? apartmentAddress)
    {
        var hasLocation = location?.GeoLocationLat is not null
            || location?.GeoLocationLng is not null
            || !string.IsNullOrWhiteSpace(apartmentAddress);

        if (!hasLocation)
        {
            return "Brak danych lokalizacji apartamentu - powiedz to wprost i nie zgaduj informacji o miejscach w pobliżu.";
        }

        return "Dla pytań o to, co jest blisko, odpowiadaj ostrożnie i używaj sformułowania: \"na podstawie lokalizacji apartamentu wygląda, że...\". Nie podawaj niezweryfikowanych, dokładnych POI.";
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en-US";
        }

        var lowered = language.Trim().ToLowerInvariant();
        return lowered switch
        {
            "pl" or "pol" or "pl-pl" => "pl-PL",
            "de" or "deu" or "de-de" => "de-DE",
            "en" or "eng" or "en-us" => "en-US",
            _ => "en-US"
        };
    }

    private static string BuildGuestName(string? firstName, string? lastName)
    {
        var full = string.Join(" ", new[] { firstName, lastName }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim()));

        return string.IsNullOrWhiteSpace(full) ? "Guest" : full;
    }

    private static string BuildInstructionsSummary(IReadOnlyList<ApartmentArrivalInstructionStepDTO> instructions)
    {
        if (instructions.Count == 0)
        {
            return "No dedicated arrival instructions are available.";
        }

        return string.Join("\n", instructions
            .OrderBy(i => i.Sequence)
            .Take(5)
            .Select(i => $"{i.Sequence}. {i.Name}: {Trim(i.Description, 180)}"));
    }

    private static string BuildRulesSummary(IReadOnlyList<CustomerTermDisplayDto> rules)
    {
        if (rules.Count == 0)
        {
            return "No rules summary is available.";
        }

        return string.Join("\n", rules
            .Take(6)
            .Select(r => $"- {Trim(r.Description, 220)}"));
    }

    private static string BuildLockInstructionsSummary(LockInstructionsDTO instructions)
    {
        var parts = new[]
        {
            instructions.CylinderOpen,
            instructions.CylinderClose,
            instructions.PanelOpen,
            instructions.PanelClose
        }
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .Select(v => v.Trim())
        .ToArray();

        return parts.Length == 0
            ? "No lock instructions are available."
            : string.Join("\n\n", parts);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var normalized = NormalizeValue(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? BuildApartmentNumberOrItemCode(ReservationItem? reservationItem)
    {
        var itemCode = NormalizeValue(reservationItem?.itemCode);
        if (!string.IsNullOrWhiteSpace(itemCode))
        {
            return itemCode;
        }

        if (reservationItem?.objectItemId > 0)
        {
            return reservationItem.objectItemId.ToString(CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? CleanToPlainText(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", " ", StringComparison.OrdinalIgnoreCase);

        var withoutHtml = HtmlTagRegex.Replace(normalized, " ");
        var decoded = WebUtility.HtmlDecode(withoutHtml);
        var singleSpaced = MultiWhitespaceRegex.Replace(decoded, " ").Trim();

        if (singleSpaced.Length == 0)
        {
            return null;
        }

        if (singleSpaced.Length <= max)
        {
            return singleSpaced;
        }

        return singleSpaced[..max] + "...";
    }

    private static string Trim(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "n/a";
        }

        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= max)
        {
            return normalized;
        }

        return normalized[..max] + "...";
    }
}
