using System.Text.Json;

namespace RentoomBooking.LiveChat.Bitrix;

internal static class BitrixRestHelpers
{
    internal static string BuildRestMethodUrl(string clientEndpoint, string method, string accessToken)
    {
        return $"{NormalizeClientEndpoint(clientEndpoint, null)}{method}?auth={Uri.EscapeDataString(accessToken)}";
    }

    internal static string NormalizeClientEndpoint(string? clientEndpoint, string? domain)
    {
        var candidate = string.IsNullOrWhiteSpace(clientEndpoint) ? domain : clientEndpoint;
        if (string.IsNullOrWhiteSpace(candidate))
            throw new InvalidOperationException("Bitrix client endpoint is not configured.");

        candidate = candidate.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            if (!Uri.TryCreate($"https://{candidate.TrimStart('/')}", UriKind.Absolute, out uri))
                throw new InvalidOperationException($"Invalid Bitrix client endpoint: {candidate}");

        var path = uri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path) || path == "/")
            path = "/rest";
        else if (!path.EndsWith("/rest", StringComparison.OrdinalIgnoreCase)) path = $"{path}/rest";

        return $"{uri.Scheme}://{uri.Authority}{path.TrimEnd('/')}/";
    }

    internal static string NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri)) return absoluteUri.Host;

        if (trimmed.Contains('/'))
            return Uri.TryCreate($"https://{trimmed.TrimStart('/')}", UriKind.Absolute, out absoluteUri)
                ? absoluteUri.Host
                : trimmed.Trim('/');

        return trimmed.Trim('/');
    }

    internal static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (property.NameEquals(propertyName) ||
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }

    internal static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property)) return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    internal static void EnsureBitrixSuccess(HttpResponseMessage response, string body, string methodName)
    {
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Bitrix {methodName} failed (HTTP {(int)response.StatusCode}): {body}");

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.TryGetProperty("error", out var errorProp))
        {
            var description = document.RootElement.TryGetProperty("error_description", out var descriptionProp)
                ? descriptionProp.ToString()
                : "Unknown Bitrix error.";
            throw new InvalidOperationException($"Bitrix {methodName} failed: {errorProp} - {description}");
        }
    }
}