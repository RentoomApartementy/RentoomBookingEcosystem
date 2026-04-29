using RentoomBooking.SharedClasses.Integrations.TTLock.Models;
using System.Net;

namespace RentoomBooking.SharedClasses.Integrations.TTLock.Services
{
    public interface ITTLockPasscodeAppService
    {
        Task<TTLockPasscodeOperationResult> GetAccessCodesAsync(string reservationToken, CancellationToken ct);
        Task<TTLockPasscodeOperationResult> GenerateAccessCodeAsync(string reservationToken, CancellationToken ct);
    }

    public sealed class TTLockPasscodeOperationResult
    {
        public HttpStatusCode StatusCode { get; init; }
        public AccessCodesResponse? Payload { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
