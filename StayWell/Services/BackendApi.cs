using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RentoomBooking.StayWell.Services
{

    public record ReservationResponse(RentoomReservation? Reservation, HttpStatusCode StatusCode);

    public class BackendApi(IHttpClientFactory factory, JsonSerializerOptions json)
    {

        private readonly HttpClient _http = factory.CreateClient("FunctionsApi");
        private readonly JsonSerializerOptions _json = json;


        public async Task<ReservationResponse?> GetReservationByTokenAsync(string token)
        {
            var response = await _http.GetAsync($"db/reservations/{token}");
            if (response.IsSuccessStatusCode)
            {
                var reservation = await response.Content.ReadFromJsonAsync<RentoomReservation>(_json);
                Console.WriteLine(response.StatusCode);
                return new(reservation!, response.StatusCode);
            }
            else
            {
                return new(null, response.StatusCode);
            }

            //throw new Exception($"{response.Content} {response.StatusCode}");
        }

        //public async Task<RentoomReservation?> GetReservationByTokenAsync(string token)
        //{
        //    return await _http.GetFromJsonAsync<RentoomReservation>($"db/reservations/{token}", _json);
        //}

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

        public async Task<ApartmentObject?> GetApartmentByIdAsync(int id)
        {
            return await _http.GetFromJsonAsync<ApartmentObject>($"db/apartments/{id}", _json);
        }

        public async Task<List<Lock>> GetLocksAsync(int reservationId, int itemId)
        {
            return await _http.GetFromJsonAsync<List<Lock>>($"locks/{reservationId}/{itemId}", _json)
                ?? [];
        }




    }
}
