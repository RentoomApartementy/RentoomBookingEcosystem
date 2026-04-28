using Newtonsoft.Json;

namespace RentoomBooking.SharedClasses.Integrations.TTLock.Models
{
    public class TTLockSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = "https://euapi.ttlock.com";
    }

    public class TTLockBaseResponse
    {
        [JsonProperty("errcode")]
        public int ErrCode { get; set; }

        [JsonProperty("errmsg")]
        public string? ErrMsg { get; set; }

        public bool IsSuccess => ErrCode == 0;
    }

    public class TTLockTokenResponse : TTLockBaseResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class TTLockElectricResponse : TTLockBaseResponse
    {
        [JsonProperty("electricQuantity")]
        public int ElectricQuantity { get; set; }
    }

    public class TTLockStateResponse : TTLockBaseResponse
    {
        /// 0: locked, 1: unlocked, 2: unknown, 3: unlocking
        [JsonProperty("state")]
        public int State { get; set; }
    }

    public class TTLockPasscodeResponse : TTLockBaseResponse
    {
        [JsonProperty("keyboardPwd")]
        public string? KeyboardPwd { get; set; }

        [JsonProperty("keyboardPwdId")]
        public int KeyboardPwdId { get; set; }
    }

    public sealed class AccessCodeDto
    {
        public string? Code { get; init; }
        public int? KeyboardPwdId { get; init; }
        public DateTimeOffset? GeneratedAt { get; init; }
        public DateTimeOffset? ValidFrom { get; init; }
        public DateTimeOffset? ValidTo { get; init; }
        public string Source { get; init; } = "TTLock";
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

    public enum TTLockKeyboardPwdType
    {
        OneTime = 1,
        Permanent = 2,
        Period = 3,
        Delete = 4,
        WeekendCyclic = 5,
        DailyCyclic = 6,
        WorkdayCyclic = 7,
        MondayCyclic = 8,
        TuesdayCyclic = 9,
        WednesdayCyclic = 10,
        ThursdayCyclic = 11,
        FridayCyclic = 12,
        SaturdayCyclic = 13,
        SundayCyclic = 14,
    }
}