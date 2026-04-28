using System;
using System.Collections.Generic;

namespace RentoomBooking.SharedClasses.Models.StayWell;

public class CityParkingInfoDto
{
    public List<ParkingZoneDto> Zones { get; set; } = [];
    public string InfoUrl { get; set; } = string.Empty;
}

public class ParkingZoneDto
{
    /// <summary>Polish display name for AI agent context.</summary>
    public string Name { get; set; } = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public TimeOnly PaidFrom { get; set; }
    public TimeOnly PaidTo { get; set; }
    public List<DayOfWeek> PaidDays { get; set; } = [];
    public List<DayOfWeek> FreeDays { get; set; } = [];
}
