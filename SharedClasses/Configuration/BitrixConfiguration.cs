using Microsoft.Extensions.Configuration;

namespace RentoomBooking.SharedClasses.Configuration;

public static class BitrixConfiguration
{
    public const string DomainKey = "Bitrix:Domain";
    public const string DomainFlatKey = "BitrixDomain";
    public const string DefaultDomain = "https://b24-grfccp.bitrix24.pl/rest";
    public const string WebhookIdKey = "Bitrix:WebhookId";
    public const string WebhookIdFlatKey = "BitrixWebhookId";
    public const string DefaultWebhookId = "n5tri19od1ylw2fn";
    public const string ReservationPipelineNameKey = "Bitrix:ReservationPipelineName";
    public const string ReservationPipelineNameFlatKey = "BitrixReservationPipelineName";
    public const string DefaultReservationPipelineName = "Rezerwacje";
    public const string UserIdForWebhookKey = "Bitrix:UserIdForWebhook";
    public const string UserIdForWebhookFlatKey = "BitrixUserIdForWebhook";
    public const string DefaultUserIdForWebhook = "208";
    public const string AssignedByUserIdKey = "Bitrix:AssignedByUserId";
    public const string AssignedByUserIdFlatKey = "BitrixAssignedByUserId";
    public const int DefaultAssignedByUserId = 208;

    public static string GetDomain(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Environment.GetEnvironmentVariable("Bitrix__Domain")
            ?? Environment.GetEnvironmentVariable(DomainFlatKey)
            ?? configuration[DomainKey]
            ?? configuration[DomainFlatKey]
            ?? DefaultDomain;
    }

    public static string GetWebhookId(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Environment.GetEnvironmentVariable("Bitrix__WebhookId")
            ?? Environment.GetEnvironmentVariable(WebhookIdFlatKey)
            ?? configuration[WebhookIdKey]
            ?? configuration[WebhookIdFlatKey]
            ?? DefaultWebhookId;
    }

    public static string GetReservationPipelineName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Environment.GetEnvironmentVariable("Bitrix__ReservationPipelineName")
            ?? Environment.GetEnvironmentVariable(ReservationPipelineNameFlatKey)
            ?? configuration[ReservationPipelineNameKey]
            ?? configuration[ReservationPipelineNameFlatKey]
            ?? DefaultReservationPipelineName;
    }

    public static string GetUserIdForWebhook(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Environment.GetEnvironmentVariable("Bitrix__UserIdForWebhook")
            ?? Environment.GetEnvironmentVariable(UserIdForWebhookFlatKey)
            ?? configuration[UserIdForWebhookKey]
            ?? configuration[UserIdForWebhookFlatKey]
            ?? DefaultUserIdForWebhook;
    }

    public static int GetAssignedByUserId(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = Environment.GetEnvironmentVariable("Bitrix__AssignedByUserId")
            ?? Environment.GetEnvironmentVariable(AssignedByUserIdFlatKey)
            ?? configuration[AssignedByUserIdKey]
            ?? configuration[AssignedByUserIdFlatKey];

        return int.TryParse(value, out var parsed)
            ? parsed
            : DefaultAssignedByUserId;
    }
}
