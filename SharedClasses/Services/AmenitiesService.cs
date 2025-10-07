using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using System.Net;
using System.Net.Http;
using System.Text;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IAmenitiesService
    {
        Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes, CancellationToken ct = default);
    }





    public class AmenitiesService : IAmenitiesService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        ApartmentRepository _AmenitiesRepository;
        public AmenitiesService(IHttpClientFactory factory, BookingDatabase bd, ApartmentRepository AmenitiesRepository)
        {
            _factory = factory;
            _bd = bd;
            _AmenitiesRepository = AmenitiesRepository;
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

        public async Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes, CancellationToken ct = default)
        {
            var amenities = await GetAmenitiesForObjectTypes(objectTypes, ct);
            if (amenities == null || amenities.Count == 0)
            {
                return amenities;
            }

            var filterValues = await GetAmenitiesFilterValuesAsync(ct);
            if (filterValues.Length == 0)
            {
                return amenities;
            }

            var filterSet = new HashSet<int>(filterValues);

            foreach (var objectTypeAmenities in amenities)
            {
                if (objectTypeAmenities?.ObjectAmenities == null)
                {
                    continue;
                }

                objectTypeAmenities.ObjectAmenities = objectTypeAmenities.ObjectAmenities
                    .Where(a => filterSet.Contains(a.AmenityId))
                    .ToList();
            }

            return amenities;
        }

        private async Task<int[]> GetAmenitiesFilterValuesAsync(CancellationToken ct)
        {
            var http = _factory.CreateClient("FunctionsApi");
            using var resp = await http.GetAsync("amenities/filter", ct);

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<int>();
            }

            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);
            var document = JsonConvert.DeserializeObject<AmenitiesFilterDocument>(jsonString);

            return document?.amenities ?? Array.Empty<int>();
        }
    }
}
