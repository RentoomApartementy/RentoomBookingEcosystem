using Microsoft.Extensions.Configuration;

namespace RentoomBooking.SharedClasses.Configuration;

public static class BitrixConfiguration
{
    public const string ReservationPipelineNameKey = "Bitrix:ReservationPipelineName";
    public const string ReservationPipelineNameFlatKey = "BitrixReservationPipelineName";
    public const string DefaultReservationPipelineName = "Rezerwacje";

    public static string GetReservationPipelineName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Environment.GetEnvironmentVariable("Bitrix__ReservationPipelineName")
            ?? Environment.GetEnvironmentVariable(ReservationPipelineNameFlatKey)
            ?? configuration[ReservationPipelineNameKey]
            ?? configuration[ReservationPipelineNameFlatKey]
            ?? DefaultReservationPipelineName;
    }
}
