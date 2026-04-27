namespace RentoomBooking.Api.LiveChat.Bitrix;

public sealed record BitrixLiveChatPortalInstallation(
    string MemberId,
    string Domain,
    string ClientEndpoint,
    string? ServerEndpoint,
    string AccessToken,
    string? RefreshToken,
    string? Scope,
    string? Status,
    string? ApplicationToken,
    DateTime? AccessTokenExpiresAt);
