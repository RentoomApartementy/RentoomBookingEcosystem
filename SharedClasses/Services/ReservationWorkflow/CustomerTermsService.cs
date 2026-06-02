using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;

namespace RentoomBooking.SharedClasses.Services.ReservationWorkflow
{
    public class CustomerTermsService
    {
        private readonly CustomerTermsRepository _repository;

        public CustomerTermsService(CustomerTermsRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CustomerTermDisplayDto>> GetTermsForDisplayAsync(string? cultureName, bool onlyRequiredForWorkflow = false, CancellationToken ct = default)
        {
            return await _repository.GetActiveTermsSourcesAsync(cultureName, onlyRequiredForWorkflow, ct);
        }

        public async Task<CustomerTermDisplayDto?> GetTermByIdAsync(int id, string? cultureName, CancellationToken ct = default)
        {
            return await _repository.GetTermByIdAsync(id, cultureName, ct);
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(Guid reservationGuid, string? cultureName = null)
        {
            return await _repository.GetAgreedTermsByReservationAsync(reservationGuid, cultureName);
        }
    }
}
