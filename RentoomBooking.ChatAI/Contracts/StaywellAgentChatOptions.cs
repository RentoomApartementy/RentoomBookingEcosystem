namespace RentoomBooking.ChatAI.Contracts;

public sealed class StaywellAgentChatOptions
{
    public const string SectionName = "StaywellAgentChat";

    public string ProjectEndpoint { get; set; } = string.Empty;
    public string AgentName { get; set; } = "staywell-events-mvp";
    public string ToolboxEndpoint { get; set; } = string.Empty;
    public string TokenScope { get; set; } = "https://ai.azure.com/.default";
    public int MaxMessageLength { get; set; } = 2000;
    public int MaxHistoryMessages { get; set; } = 15;
    public int MaxRequestsPerMinute { get; set; } = 12;
    public int StreamingTimeoutSeconds { get; set; } = 90;
}
