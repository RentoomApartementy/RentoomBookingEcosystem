using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database;
using System.Globalization;
using System.Net;

namespace RentoomBooking.Api.Events;

public class GetEventsFunction
{
    private readonly IEventReadRepository _eventReadRepository;
    private readonly ILogger<GetEventsFunction> _logger;

    public GetEventsFunction(IEventReadRepository eventReadRepository, ILogger<GetEventsFunction> logger)
    {
        _eventReadRepository = eventReadRepository;
        _logger = logger;
    }

    [Function("GetEvents")]
    public async Task<HttpResponseData> GetEvents(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/query")] HttpRequestData req)
    {
        _logger.LogInformation("GetEvents started at: {time}", DateTime.UtcNow);
        var res = req.CreateResponse();

        try
        {
            var queryParameters = ParseQuery(req.Url.Query);

            string? timeFromError = null;
            string? timeToError = null;
            if (!TryParseRequiredDate(queryParameters, "timeFrom", out var timeFrom, out timeFromError) ||
                !TryParseRequiredDate(queryParameters, "timeTo", out var timeTo, out timeToError))
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync(timeFromError ?? timeToError ?? "Invalid date range.");
                return res;
            }

            if (timeFrom >= timeTo)
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Parameter 'timeFrom' must be earlier than 'timeTo'.");
                return res;
            }

            var limit = ParseInt(queryParameters, "limit", 20, 1, 100);

            var query = new EventSearchQuery
            {
                TimeFrom = timeFrom,
                TimeTo = timeTo,
                City = TrimToNull(GetValue(queryParameters, "city")) ?? "Toruń",
                Category = TrimToNull(GetValue(queryParameters, "category")),
                Language = TrimToNull(GetValue(queryParameters, "language")) ?? "pl",
                Limit = limit
            };

            var cancellationToken = req.FunctionContext.CancellationToken;
            var events = await _eventReadRepository.GetEventsAsync(query, cancellationToken);

            res.StatusCode = HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(new
            {
                count = events.Count,
                query = new
                {
                    query.TimeFrom,
                    query.TimeTo,
                    query.City,
                    query.Category,
                    query.Language,
                    query.Limit
                },
                data = events
            }));

            return res;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GetEvents.");
            res.StatusCode = HttpStatusCode.InternalServerError;
            await res.WriteStringAsync("Internal server error.");
            return res;
        }
        finally
        {
            _logger.LogInformation("GetEvents finished at: {time}", DateTime.UtcNow);
        }
    }

    private static Dictionary<string, string?> ParseQuery(string query)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var normalized = query.StartsWith('?') ? query[1..] : query;
        var pairs = normalized.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                result[Uri.UnescapeDataString(pair)] = null;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static bool TryParseRequiredDate(Dictionary<string, string?> query, string key, out DateTime value, out string? error)
    {
        value = default;
        error = null;

        var raw = GetValue(query, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = $"Missing required query parameter '{key}'.";
            return false;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            value = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
            return true;
        }

        error = $"Invalid '{key}' format. Use ISO date-time, e.g. 2026-05-01T00:00:00.";
        return false;
    }

    private static int ParseInt(Dictionary<string, string?> query, string key, int defaultValue, int min, int max)
    {
        var raw = GetValue(query, key);
        if (!int.TryParse(raw, out var parsed))
        {
            return defaultValue;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static string? GetValue(Dictionary<string, string?> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value : null;
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
