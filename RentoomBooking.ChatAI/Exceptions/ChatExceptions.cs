namespace RentoomBooking.ChatAI.Exceptions;

public sealed class ChatValidationException : Exception
{
    public ChatValidationException(string message) : base(message)
    {
    }
}

public sealed class ChatForbiddenException : Exception
{
    public ChatForbiddenException(string message) : base(message)
    {
    }
}

public sealed class ChatNotFoundException : Exception
{
    public ChatNotFoundException(string message) : base(message)
    {
    }
}

public sealed class ChatRateLimitException : Exception
{
    public ChatRateLimitException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
