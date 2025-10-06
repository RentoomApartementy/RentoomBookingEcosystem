using RentoomBooking.SharedClasses.Models;
using System.Net.Http.Json;

namespace RentoomBooking.StayWell.Services
{
    public class ReservationService(BackendApi backendApi, ILogger<ReservationService> logger)
    {
        private readonly BackendApi _backendApi = backendApi;
        private readonly ILogger<ReservationService> _logger = logger;

        public async Task<RentoomReservation?> GetReservationByTokenAsync(string token)
        {
            try
            {
                var reservation = await _backendApi.GetReservationByTokenAsync(token);
                if (reservation == null)
                {
                    _logger.LogWarning("Rezerwacja o tokenie {Token} jest NULL", token);
                }
                return reservation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Blad podczas zaciagania rezerawcji z token: {Token}", token);
                return null;
            }
        }

    }
}
