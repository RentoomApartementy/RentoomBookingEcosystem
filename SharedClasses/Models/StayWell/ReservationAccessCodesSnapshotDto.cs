using System;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.StayWell
{
    /// <summary>
    /// Źródło kodu dostępu do apartamentu.
    /// </summary>
    public enum AccessCodeSource
    {
        TTLock,
        Ido
    }

    /// <summary>
    /// Powód blokady generowania nowego kodu.
    /// </summary>
    public enum GenerationBlockReason
    {
        None,
        ReservationNotActive,
        CooldownActive,
        SameHourConflict,
        LockNotConfigured
    }

    /// <summary>
    /// Pojedynczy kod dostępu (TTLock lub Ido).
    /// </summary>
    public class AccessCodeDto
    {
        public string? Code { get; set; }
        public int? KeyboardPwdId { get; set; }
        public DateTimeOffset? GeneratedAt { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public AccessCodeSource Source { get; set; }
    }

    /// <summary>
    /// Pełny snapshot kodów dostępu rezerwacji — zwracany z jednego endpointu API.
    /// Zawiera aktualny kod, historię, okno ważności, możliwość generowania nowego.
    /// </summary>
    public class AccessCodesSnapshotDto
    {
        public bool IsCodeAvailable { get; set; }
        public DateTimeOffset? CodeAvailableFrom { get; set; }
        public DateTimeOffset? CodeAvailableTo { get; set; }
        public bool IsAfterCheckOut { get; set; }

        public bool CanGenerateNewCode { get; set; }
        public GenerationBlockReason BlockReason { get; set; }
        public DateTimeOffset? NextGenerationAvailableAt { get; set; }
        public int CooldownSecondsRemaining { get; set; }

        public AccessCodeDto? CurrentCode { get; set; }
        public List<AccessCodeDto> History { get; set; } = [];
    }
}
