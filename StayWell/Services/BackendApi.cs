using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Cookies;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using WorkflowModels = RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.StayWell;
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
    public record ReservationResponse(RentoomReservation? Reservation, WorkflowModels.StayWellReservationRecordDto? ReservationRecord, HttpStatusCode StatusCode);

    public class BackendApi(IHttpClientFactory factory, JsonSerializerOptions json, LocalStorageService localStorage)
    {
        private readonly HttpClient _http = factory.CreateClient("FunctionsApi");
        private readonly JsonSerializerOptions _json = json;
        private readonly LocalStorageService _localStorage = localStorage;

        private const string CachePrefix = "staywell:backendapi:v1";
        private const string NullMarker = "__null__";

        private static string BuildCacheKey(string scope, params string[] parts)
        {
            return $"{CachePrefix}:{scope}:{string.Join("|", parts)}";
        }

        private T? DeserializePayload<T>(string payload) where T : class
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(payload, _json);
            }
            catch
            {
                return null;
            }
        }

        private async Task<T?> GetOrSetCacheAsync<T>(string key, Func<Task<T?>> fetch, bool cacheNull = false) where T : class
        {
            var raw = await _localStorage.GetItemAsync(key);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (cacheNull && string.Equals(raw, NullMarker, StringComparison.Ordinal))
                {
                    return null;
                }

                try
                {
                    var cached = JsonSerializer.Deserialize<T>(raw, _json);
                    if (cached is not null)
                    {
                        return cached;
                    }
                }
                catch
                {
                }
            }

            var fresh = await fetch();

            if (fresh is null)
            {
                if (cacheNull)
                {
                    await _localStorage.SetItemAsync(key, NullMarker);
                }

                return null;
            }

            await _localStorage.SetItemAsync(key, fresh);
            return fresh;
        }

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
                    var payload = await response.Content.ReadAsStringAsync();
                    var reservationLookup = DeserializePayload<WorkflowModels.StayWellReservationLookupResponse>(payload);
                    if (reservationLookup?.Reservation is not null || reservationLookup?.ReservationRecord is not null)
                    {
                        return new(reservationLookup.Reservation, reservationLookup.ReservationRecord, response.StatusCode);
                    }

                    var reservation = DeserializePayload<RentoomReservation>(payload);
                    Console.WriteLine(response.StatusCode);

                    return new(reservation, null, response.StatusCode);
                }
                else
                {
                    return new(null, null, response.StatusCode);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return new(null, null, System.Net.HttpStatusCode.InternalServerError);
            }
        }

        public async Task<List<ObjectMedium>> GetApartmentMediaAsync(int objectId)
        {
            return await _http.GetFromJsonAsync<List<ObjectMedium>>($"apartments/{objectId}/media", _json)
                   ?? [];
        }

        public async Task<List<ObjectAmenity>> GetAmenitiesForObjectsAsync(int objectId)
        {
            var cacheKey = BuildCacheKey("amenities", objectId.ToString());

            var result = await GetOrSetCacheAsync(
                cacheKey,
                async () => await _http.GetFromJsonAsync<List<ObjectAmenity>>($"apartments/{objectId}/amenities", _json) ?? []);

            return result ?? [];
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
            var cacheKey = BuildCacheKey("terms", resToken);

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var response = await _http.GetAsync($"db/terms/GetTermsByResToken/{resToken}");
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    return await response.Content.ReadFromJsonAsync<TermsEntity>(_json);
                },
                cacheNull: true);
        }

        public async Task<List<CustomerTermDisplayDto>> GetTermsForDisplayAsync(string? language = null)
        {
            var url = "db/terms/get-available";
            if (!string.IsNullOrWhiteSpace(language))
            {
                url += $"?lang={Uri.EscapeDataString(language)}";
            }

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<CustomerTermDisplayDto>>(_json)
                   ?? [];
        }

        public async Task<bool> SaveTermsAsync(TermsEntity entity)
        {
            var response = await _http.PostAsJsonAsync("db/terms/SaveTerms", entity, _json);

            var isOk = await UpdateAgreedTermAsync(entity.ResToken, 2,true);

            if (!isOk)
            {
                return false;
            }

            await InvalidateCacheAsync("terms", entity.ResToken);

            return true;
        }

        public async Task<RegistrationCardEntity?> GetRegistrationCardByResTokenAsync(string resToken)
        {
            var cacheKey = BuildCacheKey("registration-card", resToken);

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    var response = await _http.GetAsync($"db/registrationcard/GetRegistrationCardByResToken/{resToken}");
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    return await response.Content.ReadFromJsonAsync<RegistrationCardEntity>(_json);
                },
                cacheNull: true);
        }

        public async Task<bool> SaveRegistrationCardAsync(RegistrationCardEntity entity)
        {
            var response = await _http.PostAsJsonAsync("db/registrationcard/SaveRegistrationCard", entity, _json);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(entity.ResToken))
            {
                var cacheKey = BuildCacheKey("registration-card", entity.ResToken);
                await _localStorage.SetItemAsync(cacheKey, entity);
            }

            return true;
        }

        public async Task<string?> GetQrMaintFormUrlAsync(int apartmentId)
        {
            var cacheKey = BuildCacheKey("qrmaint-form-url", apartmentId.ToString());

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    using var response = await _http.GetAsync($"qrmaint/form-url/{apartmentId}");

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    response.EnsureSuccessStatusCode();

                    var result = await response.Content.ReadFromJsonAsync<QrMaintResponse>(_json);
                    return result?.url;
                });
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

        public async Task<ApartmentInstructionsDTO> GetArrivalInstructionStepsAsync(int apartmentId, string? language = null)
        {
                    var url = $"apartment/arrivalinstructions/{apartmentId}";
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        url += $"?language={Uri.EscapeDataString(language)}";
                    }

                    var response = await _http.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new ApartmentInstructionsDTO();
                    }

            return await response.Content.ReadFromJsonAsync<ApartmentInstructionsDTO>(_json)
                   ?? new ApartmentInstructionsDTO();
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
            var cacheKey = BuildCacheKey("qrmaint-wifi", apartmentId.ToString());

            return await GetOrSetCacheAsync(
                cacheKey,
                async () =>
                {
                    using var response = await _http.GetAsync($"qrmaint/wifi/{apartmentId}");

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return null;
                    }

                    response.EnsureSuccessStatusCode();

                    return await response.Content.ReadFromJsonAsync<RentoomWifiInfo>(_json);
                });
        }

        public async Task<List<DefinedAddonEntity>> GetDefinedAddonsAsync()
        {
            return await _http.GetFromJsonAsync<List<DefinedAddonEntity>>("db/definedaddons", _json)
                   ?? [];
        }

        public async Task<CityParkingInfoDto?> GetCityParkingInfoAsync(CancellationToken cancellationToken = default)
        {
            var key = BuildCacheKey("parking", "city");
            return await GetOrSetCacheAsync<CityParkingInfoDto>(key, async () =>
            {
                using var response = await _http.GetAsync("parking/city", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<CityParkingInfoDto>(_json, cancellationToken);
            }, cacheNull: true);
        }

        public async Task<List<CustomerAgreedTermDto>> GetAgreedTermsByReservationAsync(string reservationToken, string? language = null)
        {
            var url = $"db/reservations/{reservationToken}/agreed-terms";
            if (!string.IsNullOrWhiteSpace(language))
            {
                url += $"?lang={Uri.EscapeDataString(language)}";
            }

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<CustomerAgreedTermDto>>(_json)
                   ?? [];
        }

        public async Task<bool> SaveCustomerTermsAsync(string reservationTokenGuid, Dictionary<int, bool> userSelections)
        {
            var response = await _http.PostAsJsonAsync($"db/reservations/{reservationTokenGuid}/agreed-terms", userSelections, _json);
            return response.IsSuccessStatusCode;
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

        public async Task<CookieNoticeDto?> GetCookieNoticeAsync(string appCode, string? culture = null)
        {
            var url = $"db/cookies/{appCode}/notice";
            if (!string.IsNullOrWhiteSpace(culture))
            {
                url += $"?culture={Uri.EscapeDataString(culture)}";
            }

            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CookieNoticeDto>(_json);
        }

        public async Task<CookieConsentAuditResultDto?> SaveCookieConsentAsync(string appCode, SaveCookieConsentRequest request)
        {
            var response = await _http.PostAsJsonAsync($"db/cookies/{appCode}/consents", request, _json);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CookieConsentAuditResultDto>(_json);
        }

        private async Task InvalidateCacheAsync(string scope, params string[] parts)
        {
            var key = BuildCacheKey(scope, parts);
            await _localStorage.RemoveItemAsync(key);
        }

        public sealed record PasscodeDto(
            string KeyboardPwd,
            int KeyboardPwdId,
            DateTimeOffset GeneratedAt,
            DateTimeOffset StartDate,
            DateTimeOffset? EndDate);
        public enum PasscodeSource
        {
            TTLock,
            Ido
        }

        public sealed record ReservationCodeDto(
            string? Code,
            int? KeyboardPwdId,
            DateTimeOffset? GeneratedAt,
            DateTimeOffset? StartDate,
            DateTimeOffset? EndDate,
            PasscodeSource Source);

        public sealed record GeneratePasscodeRequest(
            DateTimeOffset StartDate,
            DateTimeOffset EndDate,
            string PasscodeName);

        public sealed class AccessCodeDto
        {
            public string? Code { get; init; }
            public int? KeyboardPwdId { get; init; }
            public DateTimeOffset? GeneratedAt { get; init; }
            public DateTimeOffset? ValidFrom { get; init; }
            public DateTimeOffset? ValidTo { get; init; }
            public string Source { get; init; } = "TTLock";

            [JsonIgnore]
            public bool IsTTLock => string.Equals(Source, "TTLock", StringComparison.OrdinalIgnoreCase);

            [JsonIgnore]
            public bool IsIdo => string.Equals(Source, "Ido", StringComparison.OrdinalIgnoreCase);
        }

        public sealed class AccessCodesResponse
        {
            public AccessCodeDto? CurrentCode { get; init; }
            public List<AccessCodeDto> History { get; init; } = [];
            public bool CanGenerate { get; init; }
            public string? GenerationBlockReason { get; init; }
            public int? CooldownSecondsRemaining { get; init; }
            public DateTimeOffset? NextGenerationAvailableAt { get; init; }
        }

        public async Task<AccessCodesResponse?> GetAccessCodesAsync(
            string reservationToken,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _http.GetAsync(
                    $"reservation/{reservationToken}/access-codes",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<AccessCodesResponse>(_json, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        public async Task<AccessCodesResponse?> GenerateAccessCodeAsync(
            string reservationToken,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _http.PostAsync(
                    $"reservation/{reservationToken}/access-codes/generate",
                    null,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<AccessCodesResponse>(_json, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        public async Task<PasscodeDto?> GeneratePasscodeAsync(
            string reservationToken,
            GeneratePasscodeRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"reservation/{reservationToken}/passcode/generate",
                    request,
                    _json,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<PasscodeDto>(_json, cancellationToken);
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<PasscodeDto>> GetPasscodeHistoryAsync(
            string reservationToken,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _http.GetAsync(
                    $"reservation/{reservationToken}/passcode/history",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return [];

                return await response.Content.ReadFromJsonAsync<List<PasscodeDto>>(_json, cancellationToken)
                       ?? [];
            }
            catch
            {
                return [];
            }
        }
    }
}
