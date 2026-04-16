namespace RentoomBooking.StayWell.Models.Chat;

public sealed class ChatUiMessage
{
    public string Role { get; set; } = "assistant";
    public string Markdown { get; set; } = string.Empty;
}
