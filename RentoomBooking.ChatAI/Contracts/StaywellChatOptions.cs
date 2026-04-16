namespace RentoomBooking.ChatAI.Contracts;

public sealed class StaywellChatOptions
{
    public const string SectionName = "StaywellChat";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public int MaxMessageLength { get; set; } = 2000;
    public int MaxHistoryMessages { get; set; } = 15;
    public int MaxRequestsPerMinute { get; set; } = 12;
    public int StreamingTimeoutSeconds { get; set; } = 90;
}
