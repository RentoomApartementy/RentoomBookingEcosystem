using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System.Net.Http.Json;
using System.Text.Json;

namespace RentoomBooking.StayWell.Services
{
    public class BackendApi(IHttpClientFactory factory, JsonSerializerOptions json)
    {

        private readonly HttpClient _http = factory.CreateClient("FunctionsApi");
        private readonly JsonSerializerOptions _json = json;

        public async Task<RentoomReservation?> GetReservationByTokenAsync(string token)
        {
            return await _http.GetFromJsonAsync<RentoomReservation>($"db/reservations/{token}", _json);
        }

        public async Task<List<ObjectMedium>> GetApartmentMediaAsync(int objectId)
        {
            return await _http.GetFromJsonAsync<List<ObjectMedium>>($"apartments/{objectId}/media", _json)
                   ?? [];
        }

        public async Task<List<ObjectAmenity>> GetAmenitiesForObjectsAsync(int objectId)
        {
            return await _http.GetFromJsonAsync<List<ObjectAmenity>>($"apartments/{objectId}/amenities", _json)
                   ?? [];
        }





    }
}
