namespace RentoomBooking.ChatAI.Contracts;

public sealed record StaywellApiError(
    string ErrorDescription,
    string? ErrorCustomHttpCode = null,
    int? RetryAfterSeconds = null);

public sealed record StaywellApiResponse<T>(
    bool Success,
    T? Data,
    StaywellApiError Errors)
{
    public static StaywellApiResponse<T> Ok(T data) =>
        new(true, data, new StaywellApiError("OK"));

    public static StaywellApiResponse<T> Fail(
        string errorDescription,
        string? errorCustomHttpCode = null,
        int? retryAfterSeconds = null) =>
        new(false, default, new StaywellApiError(errorDescription, errorCustomHttpCode, retryAfterSeconds));
}
