using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
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
            return await _http.GetFromJsonAsync<List<Lock>>($"idobooking/locks/{reservationId}/{itemId}", _json)
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

        public sealed class TTLockActionResult
        {
            public bool? Success { get; init; }
            public string? LockCode { get; init; }
            public string? Status { get; init; }
            public int? BatteryLevel { get; init; }

            [JsonIgnore]
            public bool IsSuccess =>
                Success == true || string.Equals(Status, "Success", StringComparison.OrdinalIgnoreCase);
        }
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
      
                return await response.Content.ReadFromJsonAsync<TTLockActionResult>(_json);
            }
            catch { return null; }
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
            var response = await _http.PostAsJsonAsync($"upsells/orders/pay", request, _json);
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

        public async Task<List<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(int apartmentId, string? language = null)
        {
            var url = $"apartment/arrivalinstructions/{apartmentId}";
            if (!string.IsNullOrWhiteSpace(language))
            {
                url += $"?language={Uri.EscapeDataString(language)}";
            }

            var response = await _http.GetAsync(url);
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
        public async Task<ApartmentItemLocalSettings?> GetApartmentItemCodesAsync(
            string reservationToken,
            CancellationToken cancellationToken = default)
        {
            using var response = await _http.GetAsync(
                $"reservation/{reservationToken}/apartmentcodes",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ApartmentItemLocalSettings>(
                _json,
                cancellationToken);
        }

        public async Task<RentoomWifiInfo?> GetApartmentWifiInfoAsync(int apartmentId)
        {
            using var response = await _http.GetAsync($"qrmaint/wifi/{apartmentId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<RentoomWifiInfo>(_json);
        }

        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync()
        {
            return await _http.GetFromJsonAsync<List<DefinedAddonEntity>>("db/definedaddons", _json)
                   ?? [];
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(string reservationToken, string? language = null)
        {
            var url = $"db/reservations/{reservationToken}/agreed-terms";
            if (!string.IsNullOrWhiteSpace(language))
            {
                url += $"?language={Uri.EscapeDataString(language)}";
            }

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<CustomerAgreedTermDto>>(_json)
                   ?? [];
        }

        public async Task<bool> UpdateAgreedTermAsync(string reservationToken, int termsSourceId, bool isAccepted)
        {
            var request = new UpdateAgreedTermRequest
            {
                TermsSourceId = termsSourceId,
                IsAccepted = isAccepted
            };

            var response = await _http.PatchAsJsonAsync($"db/reservations/{reservationToken}/agreed-terms", request, _json);
            return response.StatusCode == HttpStatusCode.NoContent;
        }
    }
}
