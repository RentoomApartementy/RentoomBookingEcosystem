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

        public async Task<List<CustomerTermDisplayDto>> GetTermsForDisplayAsync(string? cultureName, bool onlyRequiredForWorkflow = false)
        {
            return await _repository.GetActiveTermsSourcesAsync(cultureName, onlyRequiredForWorkflow);
        }

        public async Task<CustomerTermDisplayDto?> GetTermByIdAsync(int id, string? cultureName)
        {
            return await _repository.GetTermByIdAsync(id, cultureName);
        }
    }
}
