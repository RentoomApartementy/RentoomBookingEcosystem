using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services;
using System.Net.Http;
using System.Text;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IApartmentsService
    {
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncWithFunctionApi(
            string? continuationToken = null,
            int top = 50,
            CancellationToken ct = default);


        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncDirect(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default);

        Task<long> GetApartmentsCountAsync();

        Task<ObjectMediaResponseType?> GetMedia(int objectId, CancellationToken ct = default);
    }

      



    public class ApartmentsService : IApartmentsService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        ApartmentRepository _apartmentsRepository;
        public ApartmentsService(IHttpClientFactory factory, BookingDatabase bd, ApartmentRepository apartmentsRepository)
        {
            _factory = factory;
            _bd = bd;
            _apartmentsRepository = apartmentsRepository;
        }

        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncWithFunctionApi(
            string? continuationToken = null,
            int top = 50,
            CancellationToken ct = default)
        {
            var http = _factory.CreateClient("FunctionsApi");

            var sb = new StringBuilder("apartments?top=").Append(top);
            if (!string.IsNullOrWhiteSpace(continuationToken))
                sb.Append("&continuationToken=").Append(Uri.EscapeDataString(continuationToken));

            using var resp = await http.GetAsync(sb.ToString(), ct);
            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);

            
            return JsonConvert.DeserializeObject<PagedResult<ApartmentObject>>(jsonString);
        }


        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncDirect(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default)
        {
            var result = await _bd.QueryApartmentsAsync(continuationToken, top);

            return result;
        }

        public async Task<long> GetApartmentsCountAsync()
        {
            return await _apartmentsRepository.GetApartmentCountAsync();
        }


        public async Task<ObjectMediaResponseType?> GetMedia(int objectId, CancellationToken ct = default)
        {
            var http = _factory.CreateClient("FunctionsApi");
            using var resp = await http.GetAsync($"apartments/{objectId}/media", ct);
            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);
            return JsonConvert.DeserializeObject<ObjectMediaResponseType>(jsonString);
        }
    }
}
