using System.Globalization;
using Microsoft.Extensions.Logging;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Services;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;

namespace RentoomBooking.Api.Chat;

public sealed class StaywellReservationContextProvider : IReservationContextProvider
{
    private readonly IReservationWorkflowService _reservationWorkflowService;
    private readonly RappQrMaintService _qrMaintService;
    private readonly ArrivalInstructionsService _arrivalInstructionsService;
    private readonly CustomerTermsRepository _customerTermsRepository;
    private readonly ILogger<StaywellReservationContextProvider> _logger;
    private readonly IdoSellService _idosellService;
    public StaywellReservationContextProvider(
        IReservationWorkflowService reservationWorkflowService,
        RappQrMaintService qrMaintService,
        ArrivalInstructionsService arrivalInstructionsService,
        CustomerTermsRepository customerTermsRepository,
        IdoSellService idosellService,
        ILogger<StaywellReservationContextProvider> logger)
    {
        _reservationWorkflowService = reservationWorkflowService;
        _qrMaintService = qrMaintService;
        _arrivalInstructionsService = arrivalInstructionsService;
        _customerTermsRepository = customerTermsRepository;
        _idosellService = idosellService;
        _logger = logger;
    }

    public async Task<ReservationPromptContext?> GetContextAsync(int reservationId,string reservationToken, CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
        {
            return null;
        }

        var rentoomReservation = await _idosellService.FetchReservationByIDFromIdoSellAsync(reservationId, false,existingResToken: reservationToken);
        var reservation = rentoomReservation?.ReservationResponse.result.Reservations[0];
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

        string? wifiSsid = null;
        string? wifiPassword = null;
        string? instructionsSummary = null;

        if (reservationItem?.objectItemId > 0)
        {
            var wifi = await _qrMaintService.GetWifiInfoAsync(reservationItem.objectItemId, cancellationToken);
            wifiSsid = wifi?.Ssid;
            wifiPassword = wifi?.Password;

            var instructions = await _arrivalInstructionsService
                .GetArrivalInstructionStepsAsync(reservationItem.objectItemId, locale, cancellationToken);

            instructionsSummary = BuildInstructionsSummary(instructions);
        }

        var rules = await _customerTermsRepository.GetActiveTermsSourcesAsync(locale, onlyRequiredForWorkflow: false);
        var rulesSummary = BuildRulesSummary(rules);

        return new ReservationPromptContext
        {
            ReservationToken = reservationToken,
            GuestName = BuildGuestName(reservation.Client?.FirstName, reservation.Client?.LastName),
            GuestEmail = reservation.Client?.Email,
            GuestPhone = reservation.Client?.Phone,
            ApartmentName = reservationItem?.objectName,
            ApartmentAddress = null,
            CheckInDate = reservation.ReservationDetails?.getDateFrom().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            CheckOutDate = reservation.ReservationDetails?.getDateTo().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ReservationStatus = reservation.ReservationDetails?.status.ToString(),
            WifiSsid = wifiSsid,
            WifiPassword = wifiPassword,
            ArrivalInstructionsSummary = instructionsSummary,
            RulesSummary = rulesSummary,
            Locale = locale
        };
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
