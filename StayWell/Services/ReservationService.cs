using RentoomBooking.SharedClasses.Models;
using System.Net.Http.Json;

namespace RentoomBooking.StayWell.Services
{
    public class ReservationService
    {
        private readonly BackendApi _backendApi;
        private readonly ILogger<ReservationService> _logger;
        public ReservationService(BackendApi backendApi, ILogger<ReservationService> logger)
        {
            _backendApi = backendApi;
            _logger = logger;
        }

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
