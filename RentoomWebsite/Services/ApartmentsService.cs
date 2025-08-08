using Newtonsoft.Json; 
using RentoomBooking.SharedClasses.Models;
using System.Text;

namespace RentoomWebsite.Services
{
    public interface IApartmentsService
    {
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
            string? continuationToken = null,
            int top = 50,
            CancellationToken ct = default);
    }

    public class ApartmentsService : IApartmentsService
    {
        private readonly IHttpClientFactory _factory;

        public ApartmentsService(IHttpClientFactory factory)
        {
            _factory = factory;
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
    }
}
