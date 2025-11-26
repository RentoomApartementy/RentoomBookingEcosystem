using Azure;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IIdoLocksService
    {
        Task<List<Lock>?> GetLocksAsync(int reservationId, int itemId, CancellationToken ct = default);
        Task<LockResponseType?> GetLocksFullResponseAsync(int reservationId, int itemId, CancellationToken ct = default);
    }

    public class IdoLocksService : IIdoLocksService
    {
       
        private ILogger<IdoLocksService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;

        private const string LocksGetEndpoint = "locks/get/34/json";

        public IdoLocksService(IIdoBookingConnectService idoConnect, ILogger<IdoLocksService> logger)
        {
            _idoConnect = idoConnect;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
           
        }

        public async Task<List<Lock>?> GetLocksAsync(int reservationId, int itemId, CancellationToken ct = default)
        {
            _logger.LogInformation("Fetching locks for reservation {reservationId} and item {itemId}", reservationId, itemId);

            var request = new LockRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = new ResultRequestPaging
                {
                    Page = 1,
                    Number = 100
                },
                ParamsSearch = new LockParamsSearch
                {
                    ReservationId = reservationId,
                    ItemId = itemId
                }
            };

            var result = await _idoConnect.PostAsync<LockRequestType, LockResponseType>(LocksGetEndpoint, request, ct);
            return result?.Result?.Locks;
        }

        public async Task<LockResponseType?> GetLocksFullResponseAsync(int reservationId, int itemId, CancellationToken ct = default)
        {
            _logger.LogInformation("Fetching full lock response for reservation {reservationId} and item {itemId}", reservationId, itemId);

            var request = new LockRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = new ResultRequestPaging
                {
                    Page = 1,
                    Number = 100
                },
                ParamsSearch = new LockParamsSearch
                {
                    ReservationId = reservationId,
                    ItemId = itemId
                }
            };

            var result = await _idoConnect.PostAsync<LockRequestType, LockResponseType>(LocksGetEndpoint, request, ct);
            return result;
        }
    }
}
