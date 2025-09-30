using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Services;
using System.Text;

namespace RentoomWebsite.Services
{
    public interface xxIApartmentsService
    {
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
            string? continuationToken = null,
            int top = 50,
            CancellationToken ct = default);


        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncDirect(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default);
    }

     
    

    public class xxApartmentsService : xxIApartmentsService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        public xxApartmentsService(IHttpClientFactory factory, BookingDatabase bd)
        {
            _factory = factory;
            _bd = bd;
        }

        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
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

            // Deserialize using Newtonsoft
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
    }
}
