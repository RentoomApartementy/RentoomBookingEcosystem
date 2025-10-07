using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
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
        Task<List<ObjectDescription>?> GetDescriptions(int objectId, string? language = null, CancellationToken ct = default);
        Task<List<ObjectTypesAmenities>?> GetAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes, CancellationToken ct = default);
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

        public async Task<List<ObjectDescription>?> GetDescriptions(int objectId, string? language = null, CancellationToken ct = default)
        {
            var http = _factory.CreateClient("FunctionsApi");
            var urlBuilder = new StringBuilder($"apartments/{objectId}/descriptions");

            if (!string.IsNullOrWhiteSpace(language))
            {
                urlBuilder.Append("?language=").Append(Uri.EscapeDataString(language));
            }

            using var resp = await http.GetAsync(urlBuilder.ToString(), ct);

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);
            return JsonConvert.DeserializeObject<List<ObjectDescription>>(jsonString);
        }

        public async Task<List<ObjectTypesAmenities>?> GetAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes, CancellationToken ct = default)
        {
            var http = _factory.CreateClient("FunctionsApi");

            var ids = objectTypes?.Select(x => ((int)x).ToString()).ToArray() ?? Array.Empty<string>();
            var urlBuilder = new StringBuilder("amenities/getForObjects");
            if (ids.Length > 0)
            {
                urlBuilder.Append("?objectTypesIds=").Append(Uri.EscapeDataString(string.Join(",", ids)));
            }

            using var resp = await http.GetAsync(urlBuilder.ToString(), ct);
            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);
            return JsonConvert.DeserializeObject<List<ObjectTypesAmenities>>(jsonString);
        }

        /*public Task<List<ObjectTypesAmenities>?> GetAmenitiesForObjectsSelectedAsFilter(CancellationToken ct = default)
        {
            return await _apartmentsRepository.GetApartmentCountAsync();

        }*/
    }
}
