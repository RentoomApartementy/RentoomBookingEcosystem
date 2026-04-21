using RentoomBooking.ChatAI.Contracts;

namespace RentoomBooking.ChatAI.Services;

public interface IPromptBuilder
{
    string BuildSystemPrompt(ReservationPromptContext context);
}
