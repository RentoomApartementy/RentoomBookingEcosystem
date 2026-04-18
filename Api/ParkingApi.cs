using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models.StayWell;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api;

public class ParkingApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly TimeOnly PaidFrom = new(8, 0);
    private static readonly TimeOnly PaidTo = new(18, 0);

    private static readonly List<DayOfWeek> MondayToSaturday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    private static readonly List<DayOfWeek> MondayToFriday =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static readonly CityParkingInfoDto TorunParkingInfo = new()
    {
        InfoUrl = "https://mzd.torun.pl/strefa-platnego-parkowania-w-toruniu/",
        Zones =
        [
            new ParkingZoneDto
            {
                Name = "Śródmiejska Strefa Płatnego Parkowania",
                NameKey = "ZoneDowntownPaidParking",
                PaidFrom = PaidFrom,
                PaidTo = PaidTo,
                PaidDays = MondayToSaturday,
                FreeDays = [DayOfWeek.Sunday]
            },
            new ParkingZoneDto
            {
                Name = "Strefa A",
                NameKey = "ZoneA",
                PaidFrom = PaidFrom,
                PaidTo = PaidTo,
                PaidDays = MondayToFriday,
                FreeDays = [DayOfWeek.Saturday, DayOfWeek.Sunday]
            },
            new ParkingZoneDto
            {
                Name = "Strefa B",
                NameKey = "ZoneB",
                PaidFrom = PaidFrom,
                PaidTo = PaidTo,
                PaidDays = MondayToFriday,
                FreeDays = [DayOfWeek.Saturday, DayOfWeek.Sunday]
            }
        ]
    };

    [Function("GetCityParkingInfo")]
    public async Task<HttpResponseData> GetCityParkingInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "parking/city")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(TorunParkingInfo, Json));
        return response;
    }
}
