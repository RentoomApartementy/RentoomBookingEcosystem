using Microsoft.Extensions.Configuration;

namespace RentoomBooking.SharedClasses.Configuration;

public static class BitrixLiveChatConfiguration
{
    public const string OpenLineIdKey = "Bitrix:OpenLineId";
    public const string OpenLineIdFlatKey = "BitrixOpenLineId";
    public const int DefaultOpenLineId = 32;

    public const string ConnectorIdKey = "BitrixLiveChat:ConnectorId";
    public const string DefaultConnectorId = "staywell_livechat";

    public static int GetOpenLineId(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = Environment.GetEnvironmentVariable("Bitrix__OpenLineId")
            ?? Environment.GetEnvironmentVariable(OpenLineIdFlatKey)
            ?? configuration[OpenLineIdKey]
            ?? configuration[OpenLineIdFlatKey];

        return int.TryParse(value, out var parsed) ? parsed : DefaultOpenLineId;
    }

    public static string GetConnectorId(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration[ConnectorIdKey] ?? DefaultConnectorId;
    }
}
