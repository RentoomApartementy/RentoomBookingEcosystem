using System.ComponentModel.DataAnnotations;

namespace RentoomBooking.SharedClasses.Configuration;

/// <summary>
/// Strongly-typed options for the Bitrix24 Open Lines (LiveChat) integration.
/// Bind from the "Bitrix:LiveChat" configuration section.
/// </summary>
public sealed class BitrixLiveChatOptions
{
    public const string SectionName = "Bitrix:LiveChat";

    /// <summary>Bitrix24 REST API base URL (e.g. https://your-portal.bitrix24.pl/rest).</summary>
    [Required]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Connector ID registered in the Open Lines connector hub.</summary>
    [Required]
    public string ConnectorId { get; set; } = BitrixLiveChatConfiguration.DefaultConnectorId;

    /// <summary>Open Line numeric ID in Bitrix24.</summary>
    [Range(1, int.MaxValue)]
    public int OpenLineId { get; set; } = BitrixLiveChatConfiguration.DefaultOpenLineId;

    /// <summary>OAuth2 client ID for the Bitrix24 app.</summary>
    [Required]
    public string OAuthClientId { get; set; } = string.Empty;

    /// <summary>OAuth2 client secret for the Bitrix24 app.</summary>
    [Required]
    public string OAuthClientSecret { get; set; } = string.Empty;

    /// <summary>Initial OAuth2 refresh token (bootstraps token rotation).</summary>
    [Required]
    public string OAuthRefreshToken { get; set; } = string.Empty;

    /// <summary>Max guest messages per session token per minute.</summary>
    [Range(1, 1000)]
    public int MaxMessagesPerMinute { get; set; } = 30;
}
