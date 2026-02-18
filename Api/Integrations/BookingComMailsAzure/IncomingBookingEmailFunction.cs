using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RentoomBooking.Api.Integrations.BookingComMailsAzure;

public class IncomingBookingEmailFunction
{
    private readonly ILogger<IncomingBookingEmailFunction> _logger;
    public record IncomingEmailDto(
    string? MessageId,
    string? ReceivedDateTime,
    string? Subject,
    string? BodyHtml
);
    public IncomingBookingEmailFunction(ILogger<IncomingBookingEmailFunction> logger)
    {
        _logger = logger;
    }

    [Function("IncomingBookingEmailFunction")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "mail/incoming")] HttpRequestData req)
    {
        string body = await new StreamReader(req.Body).ReadToEndAsync();

        IncomingEmailDto? email;
        try
        {
            email = JsonSerializer.Deserialize<IncomingEmailDto>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload: {Payload}", body);
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON.");
            return bad;
        }

        if (email is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Empty payload.");
            return bad;
        }

        var subject = email.Subject?.Trim() ?? string.Empty;

        
        var reservationId = ExtractReservationId2(subject);

       
        var bodyLen = email.BodyHtml?.Length ?? 0;
        var bodySnippet = SafeSnippet(email.BodyHtml, 50);

        _logger.LogInformation(
            "Incoming email. MessageId={MessageId}; ReceivedDateTime={ReceivedDateTime}; ReservationId={ReservationId}; Subject=\"{Subject}\"; BodyHtmlLen={BodyHtmlLen}; BodySnippet=\"{BodySnippet}\"",
            email.MessageId,
            email.ReceivedDateTime,
            reservationId,
            subject,
            bodyLen,
            bodySnippet);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json");
        await ok.WriteStringAsync(JsonSerializer.Serialize(new
        {
            status = "ok",
            messageId = email.MessageId,
            reservationId
        }));

        return ok;
    }


    private static string? ExtractReservationId(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        // 1) GUID
        var guidMatch = Regex.Match(subject,
            @"\b[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}\b");
        if (guidMatch.Success)
            return guidMatch.Value;

        // 2) RES-123456 / RES 123456
        var resMatch = Regex.Match(subject, @"\bRES[- ]?(?<id>\d{4,})\b", RegexOptions.IgnoreCase);
        if (resMatch.Success)
            return resMatch.Groups["id"].Value;

        // 3) same cyfry (>= 6) – “ostatnia deska ratunku”
        var digitsMatch = Regex.Match(subject, @"\b(?<id>\d{6,})\b");
        if (digitsMatch.Success)
            return digitsMatch.Groups["id"].Value;

        return null;
    }

    private static string? ExtractReservationId2(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        // Przykład: "Przyjęto rezerwację nr 32293 (Booking.com: 5505086125)"
        // Wyciągnie: 32293
        var match = Regex.Match(
            subject,
            @"\bnr\s+(?<id>\d+)\s*(?=\()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (match.Success)
            return match.Groups["id"].Value;

        // fallback, gdyby czasem nie było nawiasu: "nr 32293"
        match = Regex.Match(subject, @"\bnr\s+(?<id>\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
            return match.Groups["id"].Value;

        return null;
    }

    private static string SafeSnippet(string? html, int maxLen)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // minimalne “sanity”: usuń nowe linie, utnij długość
        var compact = html.Replace("\r", " ").Replace("\n", " ").Trim();
        if (compact.Length <= maxLen) return compact;

        return compact.Substring(0, maxLen) + "...";
    }
}