using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace RentoomBooking.SharedClasses.Services
{
    public class IdoSellService
    {

        private PostgresBookingDatabase _bookingDatabase;
        private ILogger<IdoSellService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;


        private const string ReservationsGetEndpoint = "reservations/get/34/json";
        private const string ReservationsAddEndpoint = "reservations/add/34/json";
        //private const string ApartmentMediaGetEndpoint = "objects/getMedia/34/json";

        private const string AllAmenitiesGetEndpoint = "amenities/getForObjects/34/json";
        private const string RestrictionsExceptionsGetEndpoint = "restrictions/getExceptions/34/json";


        public IdoSellService(IIdoBookingConnectService idoConnect, ILogger<IdoSellService> logger, PostgresBookingDatabase bookingDatabase)//, CosmosClient cosmosClient)
        {
            _idoConnect = idoConnect;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bookingDatabase = bookingDatabase;



        }

        public async Task<(ReservationResponseFromIdoSellAPI? ReservationResponse, string? resToken)> FetchReservationByIDFromIdoSellAsync(int ReservationId, bool saveToDb, CancellationToken cancellationToken = default)
        {
            ReservationRequestIDOSellAPI request = new ReservationRequestIDOSellAPI
            {
                authenticate = _idoConnect.AuthObjectIdo(),
                result = new ResultSetup { page = 1, number = 100 },
                paramsSearch = new ReservationsParamsSearch { ids = [ReservationId] }
            };

            string? stored = null;

            var ret = await _idoConnect.PostAsync<ReservationRequestIDOSellAPI, ReservationResponseFromIdoSellAPI>(ReservationsGetEndpoint, request, cancellationToken);

            if (saveToDb && ret?.result?.Reservations != null && ret.result.Reservations.Count > 0)
            {
                var reservation = ret.result.Reservations[0];

                stored = await _bookingDatabase.SaveReservationJsonAsync(reservation, _logger);

                if (stored != null)
                {
                    _logger.LogInformation("Reservation {id} with token {stored} stored in DB.", reservation.id, stored);
                }
                else
                {
                    _logger.LogWarning("Failed to store reservation {0} in DB.", ret.id);
                }
            }

            return (ret, stored);
        }

        //public Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(string? continuationToken = null, int pageSize = 50) => _bookingDatabase.QueryApartmentsAsync(continuationToken, pageSize);

        /* public async Task<List<ObjectMedium>?> FetchObjectMediaFromIdoSellAsync(int objectId, CancellationToken cancellationToken = default)
         {

             _logger.LogInformation("FetchObjectMediaFromIdoSellAsync objectId={ObjectId}", objectId);

             var request = new ObjectMediaRequestType
             {
                 Authenticate = _idoConnect.AuthObjectIdo(),
                 ObjectId = objectId
             };

             var ret = await _idoConnect.PostAsync<ObjectMediaRequestType, ObjectMediaResponseType>(ApartmentMediaGetEndpoint, request, cancellationToken);


             return ret?.Result.ObjectMedia;
         }
             */
        /* public async Task<List<ObjectMedium>?> FetchObjectMediaFromIdoSellAsync(int objectId)
         {
             string address = baseAPIUrl + "objects/getMedia/34/json";
             _logger.LogInformation("FetchObjectMediaFromIdoSellAsync objectId={ObjectId}", objectId);

             if (string.IsNullOrWhiteSpace(systemPwd) || string.IsNullOrWhiteSpace(systemUser))
             {
                 _logger.LogError("Missing IDOBOOKING credentials.");
                 throw new InvalidOperationException("Missing IDOBOOKING credentials.");
             }

             var request = new ObjectMediaRequestType
             {
                 Authenticate = new AuthenticateType
                 {
                     SystemKey = GenerateKey(HashPassword(systemPwd)),
                     SystemLogin = systemUser,
                     Lang = "eng"
                 },
                 ObjectId = objectId
             };

             using var client = new HttpClient();
             client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

             var jsonRequest = JsonHelper.SerializeOnlyNonNullProperties(request);
             var requestString = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

             HttpResponseMessage response = await client.PostAsync(address, requestString);
             var responseContent = await response.Content.ReadAsStringAsync();

             if (!response.IsSuccessStatusCode)
             {
                 _logger.LogError("Failed to fetch media for object {ObjectId}. StatusCode: {StatusCode}. Content: {Content}", objectId, response.StatusCode, responseContent);
                 response.EnsureSuccessStatusCode();
             }

             ObjectMediaResponseType ret = JsonConvert.DeserializeObject<ObjectMediaResponseType>(responseContent);
             return ret?.Result.ObjectMedia;
         }*/

        /*    public async Task<List<ObjectDescription>?> FetchObjectDescriptionsAsync(int objectId, string? language = null, CancellationToken cancellationToken = default)
            {

                var request = new ObjectDescriptionsRequestType
                {
                    Authenticate = _idoConnect.AuthObjectIdo(),

                    ParamsSearch = new ObjectDescriptionParamsSearch
                    {
                        ObjectId = objectId,
                        Language = language,
                    }
                };


                var ret = await _idoConnect.PostAsync<ObjectDescriptionsRequestType, ObjectDescriptionsResponseType>(ApartmentDescriptionsGetEndpoint, request, cancellationToken);


                return ret?.Result.ObjectDescriptions;
            }
        */
        public async Task<List<ObjectTypesAmenities>?> FetchAmenitiesForObjectTypesAsync(IEnumerable<IdoBookingObjectType> objectTypes, CancellationToken cancellationToken = default)
        {

            var objectTypeIds = objectTypes?.Select(t => (int)t).ToList();

            _logger.LogInformation("Fetching amenities for object types: {ObjectTypes}", objectTypeIds == null ? "all" : string.Join(",", objectTypeIds));

            var request = new AmenitiesForObjectsRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectTypesIds = objectTypeIds != null && objectTypeIds.Count > 0 ? objectTypeIds : null
            };

            var ret = await _idoConnect.PostAsync<AmenitiesForObjectsRequestType, AmenitiesForObjectsResponseType>(AllAmenitiesGetEndpoint, request, cancellationToken);


            /*
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch amenities for object types. StatusCode: {StatusCode}. Content: {Content}", response.StatusCode, responseContent);
                response.EnsureSuccessStatusCode();
            }
            */

            return ret?.Result.ObjectTypesAmenities;
        }

        public async Task<List<RestrictionException>?> FetchRestrictionsExceptionsAsync(GetRestrictionException? parameters = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching restrictions exceptions with filters: {Filters}", parameters);

            var request = new GetRestrictionsExceptionsRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                GetRestrictionException = parameters ?? new GetRestrictionException()
            };

            var response = await _idoConnect.PostAsync<GetRestrictionsExceptionsRequestType, GetRestrictionsExceptionsResponseType>(RestrictionsExceptionsGetEndpoint, request, cancellationToken);

            return response?.Result.GetRestrictionExceptions;
        }

        public Task<ReservationAddResponse?> AddReservationAsync(NewReservation reservation, CancellationToken cancellationToken = default)
        {
            if (reservation is null)
            {
                throw new ArgumentNullException(nameof(reservation));
            }

            return AddReservationsAsync([reservation], cancellationToken);
        }

        public async Task<ReservationAddResponse?> AddReservationsAsync(IEnumerable<NewReservation> reservations, CancellationToken cancellationToken = default)
        {
            if (reservations is null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            var reservationsList = reservations.ToList();
            if (reservationsList.Count == 0)
            {
                throw new ArgumentException("Dodaj przynajmniej jedną rezerwację.", nameof(reservations));
            }

            var request = new ReservationAddRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new ReservationAddParams
                {
                    Reservations = reservationsList
                }
            };

            var response = await _idoConnect.PostAsync<ReservationAddRequest, ReservationAddResponseType>(ReservationsAddEndpoint, request, cancellationToken);
            return response?.Result;
        }

    }
}
