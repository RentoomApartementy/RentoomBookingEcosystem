using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Models.Upsell.StayWell;
using RentoomBooking.StayWell.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return new(null, System.Net.HttpStatusCode.InternalServerError);
            }
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

        public async Task<AvailableUpsellsResponseDto?> GetAvailableUpsellsByReservationTokenAsync(string token, string? locale = null)
        {
            var url = $"db/reservations/{token}/upsells/available";
            if (!string.IsNullOrEmpty(locale))
            {
                url += $"?locale={Uri.EscapeDataString(locale)}";
            }

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            return await response.Content.ReadFromJsonAsync<AvailableUpsellsResponseDto>(_json)
                   ?? null;
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

        public async Task<bool> SaveTermsAsync(TermsEntity entity)
        {
            var response = await _http.PostAsJsonAsync("db/terms/SaveTerms", entity, _json);
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

        public async Task<string?> GetLockCodeAsync(
            int apartmentItemId,
            CancellationToken cancellationToken = default)
        {
            using var response = await _http.GetAsync(
                $"lock-code/{apartmentItemId}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LockCodeResponse>(
                _json,
                cancellationToken);

            return result?.LockCode;
        }

        public async Task<RedeemResultDto?> ValidateUpsellVoucherAsync(string reservationToken, string? codeShort, string? qrToken)
        {
            var request = new UpsellVoucherLookupRequestDto
            {
                CodeShort = codeShort,
                QrToken = qrToken,
                ReservationToken = reservationToken
            };

            var response = await _http.PostAsJsonAsync("db/upsells/vouchers/validate", request, _json);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RedeemResultDto>(_json);
        }

        public async Task<RedeemResultDto?> RedeemUpsellVoucherAsync(string reservationToken, string? codeShort, string? qrToken)
        {
            var request = new UpsellVoucherLookupRequestDto
            {
                CodeShort = codeShort,
                QrToken = qrToken,
                ReservationToken = reservationToken
            };

            var response = await _http.PostAsJsonAsync("db/upsells/vouchers/redeem", request, _json);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<RedeemResultDto>(_json);
        }

        public record TTLockActionResult(bool Success, string? LockCode, string? Status, int? BatteryLevel);
        public async Task<TTLockActionResult?> PingLockAsync(string reservationToken)
        {
            try
            {
                var response = await _http.GetAsync($"PingLockByReservationId/{reservationToken}");
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<TTLockActionResult>(_json);
            }
            catch { return null; }
        }

        public async Task<TTLockActionResult?> OpenLockAsync(string reservationToken)
        {
            try
            {
                var response = await _http.GetAsync($"OpenLockByReservationId/{reservationToken}");
                if (!response.IsSuccessStatusCode) return null;

                return await response.Content.ReadFromJsonAsync<TTLockActionResult>(_json);
            }
            catch { return null; }
        }

        public async Task<TTLockActionResult?> CloseLockAsync(string reservationToken)
        {
            try
            {
                var response = await _http.GetAsync($"CloseLockByReservationId/{reservationToken}");
                if (!response.IsSuccessStatusCode) return null;
        public async Task<UpsellPaymentInitResult?> CreateUpsellOrderAsync(string token, UpsellOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync($"reservations/{token}/upsells/orders", request, _json);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellPaymentInitResult>(_json);
        }
     
     
        //Create Order and Pay
        public async Task<UpsellPaymentInitResult?> PayUpsellOrderAsync(UpsellOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync($"upsells/orders/pay", request,_json);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellPaymentInitResult>(_json);
        }

        public async Task<UpsellOrderRecord?> GetUpsellOrderStatusAsync(Guid upsellOrderGuid)
        {
            var response = await _http.GetAsync($"upsells/orders/{upsellOrderGuid}/status");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellOrderRecord>(_json);
        }




    }

                return await response.Content.ReadFromJsonAsync<TTLockActionResult>(_json);
            }
            catch { return null; }
        }

        public async Task<List<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(int apartmentId)
        {
            var response = await _http.GetAsync($"apartment/arrivalinstructions/{apartmentId}");
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<ApartmentArrivalInstructionStepDTO>>(_json)
                   ?? [];
        }

        private sealed class LockCodeResponse
        {
            [JsonPropertyName("lockCode")]
            public string? LockCode
            {
                get; init;

            }
        }
        public async Task<UpsellPaymentInitResult?> CreateUpsellOrderAsync(string token, UpsellOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync($"reservations/{token}/upsells/orders", request, _json);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellPaymentInitResult>(_json);
        }
     
     
        //Create Order and Pay
        public async Task<UpsellPaymentInitResult?> PayUpsellOrderAsync(UpsellOrderRequest request)
        {
            var response = await _http.PostAsJsonAsync($"upsells/orders/pay", request,_json);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellPaymentInitResult>(_json);
        }

        public async Task<UpsellOrderRecord?> GetUpsellOrderStatusAsync(Guid upsellOrderGuid)
        {
            var response = await _http.GetAsync($"upsells/orders/{upsellOrderGuid}/status");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UpsellOrderRecord>(_json);
        }




    }
}