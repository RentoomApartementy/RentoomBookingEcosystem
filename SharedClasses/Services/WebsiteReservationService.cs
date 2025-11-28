using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBooking.SharedClasses.Services
{
    public class WebsiteReservationService
    {
        private readonly IClientService _clientService;
        private readonly IdoSellService _idoSellService;
        private readonly BitrixService _bitrixService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebsiteReservationService> _logger;

        public WebsiteReservationService(
            IClientService clientService,
            IdoSellService idoSellService,
            BitrixService bitrixService,
            IConfiguration configuration,
            ILogger<WebsiteReservationService> logger)
        {
            _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
            _idoSellService = idoSellService ?? throw new ArgumentNullException(nameof(idoSellService));
            _bitrixService = bitrixService ?? throw new ArgumentNullException(nameof(bitrixService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<WebsiteCreateReservationResult> CreateReservationAsync(WebsiteCreateReservationRequest request, CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Client is null)
            {
                throw new ArgumentNullException(nameof(request.Client));
            }

            if (request.Reservation is null)
            {
                throw new ArgumentNullException(nameof(request.Reservation));
            }

            _logger.LogInformation("Starting website reservation workflow.");

            var clientResult = await _clientService.AddClientAsync(request.Client, cancellationToken);

            var createdClient = clientResult?.Clients?.FirstOrDefault(c => c.Success);
            if (createdClient?.ClientId is null)
            {
                _logger.LogWarning("Failed to create client for website reservation. Error: {Error}", clientResult?.Errors?.FaultString);
                return new WebsiteCreateReservationResult
                {
                    Success = false,
                    Message = "Failed to create client in IdoBooking.",
                    ClientError = clientResult?.Errors?.FaultString ?? clientResult?.Clients?.FirstOrDefault()?.Error?.FaultString
                };
            }

            _logger.LogInformation("Created client {ClientId} for website reservation.", createdClient.ClientId);

            
            request.Reservation.ClientId = createdClient.ClientId;
            request.Reservation.ClientData = null;

            var reservationResult = await _idoSellService.AddReservationAsync(request.Reservation, cancellationToken);
            var createdReservation = reservationResult?.Reservations?.FirstOrDefault(r => r.Success);

            if (createdReservation?.ReservationId is null)
            {
                _logger.LogWarning("Failed to create reservation for client {ClientId}. Error: {Error}", createdClient.ClientId, reservationResult?.Errors?.FaultString);
                return new WebsiteCreateReservationResult
                {
                    Success = false,
                    Message = "Failed to create reservation in IdoBooking.",
                    ClientId = createdClient.ClientId,
                    ReservationError = reservationResult?.Errors?.FaultString ?? reservationResult?.Reservations?.FirstOrDefault()?.Error?.FaultString
                };
            }

            _logger.LogInformation("Created reservation {ReservationId} for client {ClientId}.", createdReservation.ReservationId, createdClient.ClientId);

            var clientBitrixResult = await _bitrixService.AddContactAsync(request.Client, createdReservation.ReservationId.Value,208);

            _logger.LogInformation("Created Bitrix CRM Client Entry {clientBitrixResult} for client {ClientId}.", clientBitrixResult, createdClient.ClientId);

            var reservationWithToken = await _idoSellService.FetchReservationByIDFromIdoSellAsync(createdReservation.ReservationId.Value, true, cancellationToken);

            var stayWellLink = BuildStayWellLink(reservationWithToken.resToken);

            return new WebsiteCreateReservationResult
            {
                Success = true,
                Message = "Reservation created successfully.",
                ClientId = createdClient.ClientId,
                BitrixClientId = clientBitrixResult,
                ReservationId = createdReservation.ReservationId,
                ResToken = reservationWithToken.resToken,
                StayWellLink = stayWellLink
            };
        }

        private string? BuildStayWellLink(string? resToken)
        {
            var baseUrl = _configuration["StayWell:ReservationUrlBase"] ?? _configuration["StayWellReservationUrlBase"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(resToken))
            {
                return null;
            }

            return baseUrl.Replace("{resToken}",resToken).TrimEnd('/');
        }
    }
}