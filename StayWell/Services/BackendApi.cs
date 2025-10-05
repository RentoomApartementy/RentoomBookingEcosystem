using RentoomBooking.SharedClasses.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace RentoomBooking.StayWell.Services
{
    public class BackendApi
    {

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _json;

        public BackendApi(IHttpClientFactory factory, JsonSerializerOptions json)
        {
            _http = factory.CreateClient("FunctionsApi");
            _json = json;
        }

        public async Task<RentoomReservation?> GetReservationByTokenAsync(string token)
        {
            return await _http.GetFromJsonAsync<RentoomReservation>($"db/reservations/{token}", _json);
        }





    }
}
