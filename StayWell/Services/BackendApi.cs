using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
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

        private class QrMaintResponse
        {
            public string? url { get; set; }
        }


        public async Task<ReservationResponse?> GetReservationByTokenAsync(string token)
        {
            try
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
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
                return new(null, System.Net.HttpStatusCode.InternalServerError);
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

        public async Task<UpsellPurchasedSummaryDto?> GetPurchasedUpsellsByReservationTokenAsync(string token)
        {
            var response = await _http.GetAsync($"db/reservations/{token}/upsells/purchased");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<UpsellPurchasedSummaryDto>(_json);
            }
            return null;
        }

        public async Task<AvailableUpsellsResponseDto?> GetAvailableUpsellsByReservationTokenAsync(string token)
        {
            var response = await _http.GetAsync($"db/reservations/{token}/upsells/available");
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadFromJsonAsync<AvailableUpsellsResponseDto>(_json);
            
            
        }

        


        public async Task<TermsEntity?> GetTermsByResTokenAsync(string resToken)
        {
            var response = await _http.GetAsync($"db/terms/GetTermsByResToken/{resToken}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TermsEntity>(_json);
            }
            return null;
        }

        public async Task<bool> AddTermsAsync(TermsEntity entity)
        {
            var response = await _http.PostAsJsonAsync("db/terms/AddTerms", entity, _json);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateTermsAsync(string resToken, TermsEntity entity)
        {
            var response = await _http.PutAsJsonAsync($"db/terms/UpdateTerms/{resToken}", entity, _json);
            return response.IsSuccessStatusCode;
        }

        public async Task<RegistrationCardEntity?> GetRegistrationCardByResTokenAsync(string resToken)
        {
            var response = await _http.GetAsync($"db/registrationcard/GetRegistrationCardByResToken/{resToken}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<RegistrationCardEntity>(_json);
            }
            return null;
        }

        public async Task<bool> SaveRegistrationCardAsync(RegistrationCardEntity entity)
        {
            var response = await _http.PostAsJsonAsync("db/registrationcard/SaveRegistrationCard", entity, _json);
            return response.IsSuccessStatusCode;
        }

        public async Task<string?> GetQrMaintFormUrlAsync(int apartmentId)
        {
            using var response = await _http.GetAsync($"qrmaint/form-url/{apartmentId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<QrMaintResponse>(_json);
            return result?.url;
        }

    }

}
