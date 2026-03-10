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

        public async Task<List<CustomerTermDisplayDto>> GetTermsForDisplayAsync(string? cultureName)
        {
            return await _repository.GetActiveTermsSourcesAsync(cultureName);
        }
    }
}
