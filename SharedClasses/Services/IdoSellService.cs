using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Payments;
using RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom;
using RentoomBooking.SharedClasses.Models.IdoBooking.ReservationManagement;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        private readonly ApartmentRepository _apartmentRepository;
        private readonly bool _useDummyIdoBooking;
        private readonly bool _bookingProcessingFlag;
        private readonly string _dummyReservationTemplateKey;


        private const string ReservationsGetEndpoint = "reservations/get/34/json";
        private const string ReservationsAddEndpoint = "reservations/add/34/json";
        private const string ReservationsEditEndpoint = "reservations/edit/34/json";
        private const string ReservationsEditStatusEndpoint = "reservations/editStatus/34/json";
        private const string ReservationsSetDiscountEndpoint = "reservations/setDiscount/34/json";
        private const string ReservationsGetSourcesEndpoint = "reservations/getSources/35/json";

        private const string PaymentsAddEndpoint = "payments/add/34/json";
        private const string PaymentsCancelEndpoint = "payments/cancel/34/json";
        private const string PaymentsConfirmEndpoint = "payments/confirm/34/json";
        private const string PaymentsEditEndpoint = "payments/edit/34/json";
        private const string PaymentsFormsEndpoint = "payments/getPaymentForms/34/json";
        private const string PaymentsGetEndpoint = "payments/get/34/json";

        //private const string ApartmentMediaGetEndpoint = "objects/getMedia/34/json";

        private const string AllAmenitiesGetEndpoint = "amenities/getForObjects/34/json";
        private const string RestrictionsExceptionsGetEndpoint = "restrictions/getExceptions/34/json";
        private const string AvailabilityLocksGetEndpoint = "availabilitylocks/get/34/json";


        public IdoSellService(
            IIdoBookingConnectService idoConnect,
            ILogger<IdoSellService> logger,
            PostgresBookingDatabase bookingDatabase,
            ApartmentRepository apartmentRepository,
            IConfiguration configuration)
        {
            _idoConnect = idoConnect;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _bookingDatabase = bookingDatabase;
            _apartmentRepository = apartmentRepository ?? throw new ArgumentNullException(nameof(apartmentRepository));
            _useDummyIdoBooking = configuration.GetValue("IdoBooking:UseDummy", false);
            _bookingProcessingFlag = configuration.GetValue("BookingCom:ReservationProcessingEnabled", false);
            _dummyReservationTemplateKey = configuration.GetValue<string>("IdoBooking:DummyReservationTemplateKey") ?? "default";

        }

        public bool UseDummyIdoBooking => _useDummyIdoBooking;

        public async Task<RentoomReservationHashRecord> FetchReservationByIDFromIdoSellAsync(int ReservationId, bool saveToDb, string? existingResToken = null, CancellationToken cancellationToken = default)
        {
            if (_useDummyIdoBooking && !_bookingProcessingFlag)
            {
                var reservation = await _bookingDatabase.GetReservationByIdAsync(ReservationId, _logger, cancellationToken);
                var response = new ReservationResponseFromIdoSellAPI
                {
                    result = new ReservationsResult
                    {
                        Authenticate = _idoConnect.AuthObjectIdo(),
                        Reservations = reservation is null ? new List<Reservation>() : new List<Reservation> { reservation },
                        errors = reservation is null
                            ? new GateErrorType { FaultString = $"Reservation {ReservationId} not found in dummy storage." }
                            : null
                    }
                };

                return new RentoomReservationHashRecord
                {
                    ReservationResponse = response,
                    resToken = existingResToken
                };
            }

            ReservationRequestIDOSellAPI request = new ReservationRequestIDOSellAPI
            {
                authenticate = _idoConnect.AuthObjectIdo(),
                result = new ResultSetup { page = 1, number = 100 },
                paramsSearch = new ReservationsParamsSearch { ids = [ReservationId] }
            };

            string? stored = null;

            var ret = await _idoConnect.PostAsync<ReservationRequestIDOSellAPI, ReservationResponseFromIdoSellAPI>(ReservationsGetEndpoint, request, cancellationToken);

            if(ret.result.errors !=null)
                throw new ApplicationException(ret.result.errors.FaultString);

            if (saveToDb && ret?.result?.Reservations != null && ret.result.Reservations.Count > 0)
            {
                var reservation = ret.result.Reservations[0];

                stored = await _bookingDatabase.SaveReservationJsonAsync(reservation, _logger, existingResToken);

                if (stored != null)
                {
                    _logger.LogInformation("Reservation {id} with token {stored} stored in DB.", reservation.id, stored);
                }
                else
                {
                    _logger.LogWarning("Failed to store reservation {0} in DB.", ret.id);
                }
            }

            return new RentoomReservationHashRecord() { ReservationResponse= ret,resToken= stored };
        }

        public async Task<IReadOnlyDictionary<int, Reservation>> FetchReservationsByIDsFromIdoSellAsync(
            IEnumerable<int> reservationIds,
            bool saveToDb,
            IReadOnlyDictionary<int, string?>? existingResTokens = null,
            CancellationToken cancellationToken = default)
        {
            if (reservationIds is null)
            {
                throw new ArgumentNullException(nameof(reservationIds));
            }

            var reservationIdList = reservationIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (reservationIdList.Count == 0)
            {
                return new Dictionary<int, Reservation>();
            }

            /*if (_useDummyIdoBooking && !_bookingProcessingFlag)
            {
                var dummyReservations = new Dictionary<int, Reservation>();

                foreach (var reservationId in reservationIdList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var reservation = await _bookingDatabase.GetReservationByIdAsync(reservationId, _logger, cancellationToken);
                    if (reservation is null)
                    {
                        continue;
                    }

                    dummyReservations[reservationId] = reservation;
                }

                return dummyReservations;
            }*/

            var request = new ReservationRequestIDOSellAPI
            {
                authenticate = _idoConnect.AuthObjectIdo(),
                result = new ResultSetup { page = 1, number = reservationIdList.Count },
                paramsSearch = new ReservationsParamsSearch { ids = reservationIdList.ToArray() }
            };

            var response = await _idoConnect.PostAsync<ReservationRequestIDOSellAPI, ReservationResponseFromIdoSellAPI>(
                ReservationsGetEndpoint,
                request,
                cancellationToken);

            if (response.result.errors != null)
            {
                throw new ApplicationException(response.result.errors.FaultString);
            }

            var reservations = response?.result?.Reservations ?? new List<Reservation>();
            var reservationMap = reservations
                .Where(reservation => reservation.id > 0)
                .GroupBy(reservation => reservation.id)
                .ToDictionary(group => group.Key, group => group.First());

            if (saveToDb && reservationMap.Count > 0)
            {
                foreach (var reservation in reservationMap.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string? existingResToken = null;
                    existingResTokens?.TryGetValue(reservation.id, out existingResToken);
                    var stored = await _bookingDatabase.SaveReservationJsonAsync(reservation, _logger, existingResToken);

                    if (!string.IsNullOrWhiteSpace(stored))
                    {
                        _logger.LogInformation("Reservation {ReservationId} with token {ReservationToken} stored in DB.", reservation.id, stored);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to store reservation {ReservationId} in DB.", reservation.id);
                    }
                }
            }

            return reservationMap;
        }

        public async Task<IReadOnlyList<Reservation>> FetchReservationByAddDateRangeFromIdoSellAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            if (endDate.Date < startDate.Date)
            {
                throw new ArgumentException("endDate must be greater than or equal to startDate.");
            }

            const int pageSize = 100;
            var reservations = new List<Reservation>();
            var uniqueReservationIds = new HashSet<int>();
            var currentPage = 1;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var request = new ReservationRequestIDOSellAPI
                {
                    authenticate = _idoConnect.AuthObjectIdo(),
                    result = new ResultSetup
                    {
                        page = currentPage,
                        number = pageSize
                    },
                    paramsSearch = new ReservationsParamsSearch
                    {
                        addDateRange = new FromToDateRange
                        {
                            startDate = startDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                            endDate = endDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                        }
                    }
                };

                var response = await _idoConnect.PostAsync<ReservationRequestIDOSellAPI, ReservationResponseFromIdoSellAPI>(
                    ReservationsGetEndpoint,
                    request,
                    cancellationToken);

                if (response?.result?.errors != null)
                {
                    throw new ApplicationException(response.result.errors.FaultString);
                }

                var pageReservations = response?.result?.Reservations ?? new List<Reservation>();

                foreach (var reservation in pageReservations)
                {
                    if (reservation.id > 0 && uniqueReservationIds.Add(reservation.id))
                    {
                        reservations.Add(reservation);
                    }
                }

                var pageAll = response?.result?.Result?.pageAll ?? currentPage;
                if (currentPage >= pageAll || pageReservations.Count == 0)
                {
                    break;
                }

                currentPage++;
            }

            return reservations;
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

        public async Task<List<AvailabilityLock>?> FetchAvailabilityLocksAsync(GetAvailabilityLocksRequestPayload? payload = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching availability locks with filters: {Filters}", payload?.ParamsSearch);

            var request = new GetAvailabilityLocksRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = payload?.Result ?? new ResultRequestPaging
                {
                    Page = 1,
                    Number = 100
                },
                ParamsSearch = payload?.ParamsSearch
            };

            var response = await _idoConnect.PostAsync<GetAvailabilityLocksRequestType, GetAvailabilityLocksResponseType>(AvailabilityLocksGetEndpoint, request, cancellationToken);
            return response?.Result.AvailabilityLocks;
        }

        public async Task<ReservationAddResponse?> AddReservationAsync(NewReservation reservation , CancellationToken cancellationToken = default)
        {
            if (reservation is null)
            {
                throw new ArgumentNullException(nameof(reservation));
            }

            return await AddReservationsAsync([reservation], cancellationToken);
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

            if (_useDummyIdoBooking)
            {
                return await AddReservationsDummyAsync(reservationsList, cancellationToken);
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

        public async Task<ReservationEditResponse?> EditReservationAsync(EditReservation reservation, CancellationToken cancellationToken = default)
        {
            if (reservation is null)
            {
                throw new ArgumentNullException(nameof(reservation));
            }

            return await EditReservationsAsync([reservation], cancellationToken);
        }

        public async Task<ReservationEditResponse?> EditReservationsAsync(IEnumerable<EditReservation> reservations, CancellationToken cancellationToken = default)
        {
            if (reservations is null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            var reservationsList = reservations.ToList();
            if (reservationsList.Count == 0)
            {
                throw new ArgumentException("Dodaj przynajmniej jedną rezerwację do edycji.", nameof(reservations));
            }

            if (_useDummyIdoBooking)
            {
                var results = new List<ReservationEditResult>();

                foreach (var reservationEdit in reservationsList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var reservation = await _bookingDatabase.GetReservationByIdAsync(reservationEdit.Id, _logger, cancellationToken);
                    if (reservation is null)
                    {
                        results.Add(new ReservationEditResult
                        {
                            ReservationId = reservationEdit.Id,
                            Success = false,
                            Error = new GateErrorType
                            {
                                FaultCode = 404,
                                FaultString = $"Reservation with id {reservationEdit.Id} not found in dummy storage."
                            }
                        });
                        continue;
                    }

                    ApplyEditToReservation(reservation, reservationEdit);

                    var updated = await _bookingDatabase.UpdateReservationJsonAsync(reservation, _logger, cancellationToken);
                    results.Add(new ReservationEditResult
                    {
                        ReservationId = reservationEdit.Id,
                        Success = updated,
                        Error = updated
                            ? null
                            : new GateErrorType
                            {
                                FaultCode = 500,
                                FaultString = $"Failed to persist edited reservation {reservationEdit.Id} in dummy storage."
                            }
                    });
                }

                return new ReservationEditResponse
                {
                    Reservations = results
                };
            }

            var request = new ReservationEditRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new ReservationEditParams
                {
                    Reservations = reservationsList
                }
            };

            var response = await _idoConnect.PostAsync<ReservationEditRequest, ReservationEditResponseType>(ReservationsEditEndpoint, request, cancellationToken);
            return response?.Result;
        }

        public async Task<ChangeReservationsStatusResponse?> ChangeReservationStatusAsync(EditReservationsStatusRequest editReservationStatusRequest, CancellationToken cancellationToken = default)
        {
            if (editReservationStatusRequest is null)
            {
                throw new ArgumentNullException(nameof(editReservationStatusRequest));
            }

            return await ChangeReservationsStatusAsync([editReservationStatusRequest], cancellationToken);
        }

        public async Task<ChangeReservationsStatusResponse?> ChangeReservationsStatusAsync(IEnumerable<EditReservationsStatusRequest> reservations, CancellationToken cancellationToken = default)
        {
            if (reservations is null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            var reservationsList = reservations.ToList();
            if (reservationsList.Count == 0)
            {
                throw new ArgumentException("Dodaj przynajmniej jedną rezerwację do zmiany statusu.", nameof(reservations));
            }

            if (_useDummyIdoBooking)
            {
                var res = reservations.ToList()[0];
                _bookingDatabase.UpdateReservationStatusInWorkflow(res.ReservationId,res.Status);

                return new ChangeReservationsStatusResponse
                {
                    Reservations = reservationsList.Select(r => new ReservationStatusChangeResult
                    {
                        ReservationId = r.ReservationId,
                        Success = true
                    }).ToList()
                };
            }

            var request = new EditReservationsStatusRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Reservations = reservationsList
            };

            var response = await _idoConnect.PostAsync<EditReservationsStatusRequestType, ChangeReservationsStatusResponseType>(ReservationsEditStatusEndpoint, request, cancellationToken);

            return response?.Result;
        }

        public async Task<ReservationSetDiscountResponse?> SetReservationDiscountAsync(SetReservationDiscount reservationDiscount, CancellationToken cancellationToken = default)
        {
            if (reservationDiscount is null)
            {
                throw new ArgumentNullException(nameof(reservationDiscount));
            }

            return await SetReservationsDiscountAsync([reservationDiscount], cancellationToken);
        }

        public async Task<ReservationSetDiscountResponse?> SetReservationsDiscountAsync(IEnumerable<SetReservationDiscount> reservationDiscounts, CancellationToken cancellationToken = default)
        {
            if (reservationDiscounts is null)
            {
                throw new ArgumentNullException(nameof(reservationDiscounts));
            }

            var reservationDiscountList = reservationDiscounts.ToList();
            if (reservationDiscountList.Count == 0)
            {
                throw new ArgumentException("Podaj przynajmniej jedną rezerwację do ustawienia rabatu.", nameof(reservationDiscounts));
            }

            if (_useDummyIdoBooking)
            {
                return await SetReservationsDiscountDummyAsync(reservationDiscountList, cancellationToken);
            }

            var request = new ReservationSetDiscountRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Reservations = reservationDiscountList
            };

            var response = await _idoConnect.PostAsync<ReservationSetDiscountRequest, ReservationSetDiscountResponseType>(
                ReservationsSetDiscountEndpoint,
                request,
                cancellationToken);

            return response?.Result;
        }

        public async Task<ReservationSourcesResponse?> GetReservationSourcesAsync(
            ReservationSourcesResultRequest? result = null,
            CancellationToken cancellationToken = default)
        {
          
            var request = new ReservationSourcesRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = result
            };

            var response = await _idoConnect.PostAsync<ReservationSourcesRequest, ReservationSourcesResponseType>(
                ReservationsGetSourcesEndpoint,
                request,
                cancellationToken);

            return response?.Result;
        }

        public async Task<PaymentAddResponse?> AddPaymentAsync(PaymentAdd payment, CancellationToken cancellationToken = default)
        {
            if (payment is null)
            {
                throw new ArgumentNullException(nameof(payment));
            }

            return await AddPaymentsAsync(new[] { payment }, cancellationToken);
        }

        public async Task<PaymentAddResponse?> AddPaymentsAsync(IEnumerable<PaymentAdd> payments, CancellationToken cancellationToken = default)
        {
            if (payments is null)
            {
                throw new ArgumentNullException(nameof(payments));
            }

            var paymentList = payments.ToList();

            if (paymentList.Count == 0)
            {
                throw new ArgumentException("Dodaj przynajmniej jedną płatność.", nameof(payments));
            }

            if (_useDummyIdoBooking)
            {
                return new PaymentAddResponse
                {
                    Results = paymentList.Select(payment => new PaymentAddResult
                    {
                        Id = GenerateReservationId(),
                        ReservationId = payment.ReservationId
                    }).ToList()
                };
            }

            var request = new PaymentAddRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new PaymentAddParams
                {
                    Payments = paymentList
                }
            };

            var response = await _idoConnect.PostAsync<PaymentAddRequest, PaymentAddResponseType>(PaymentsAddEndpoint, request, cancellationToken);

            return response?.Result;
        }

        public async Task<PaymentActionResponse?> CancelPaymentsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            if (_useDummyIdoBooking)
            {
                return BuildDummyPaymentActionResponse(paymentIds);
            }

            return await ExecutePaymentActionAsync(paymentIds, PaymentsCancelEndpoint, cancellationToken);
        }

        public async Task<PaymentActionResponse?> ConfirmPaymentsAsync(IEnumerable<int> paymentIds, CancellationToken cancellationToken = default)
        {
            if (_useDummyIdoBooking)
            {
                return BuildDummyPaymentActionResponse(paymentIds);
            }

            return await ExecutePaymentActionAsync(paymentIds, PaymentsConfirmEndpoint, cancellationToken);
        }

        public async Task<PaymentActionResponse?> EditPaymentsAsync(IEnumerable<PaymentEdit> payments, CancellationToken cancellationToken = default)
        {
            if (payments is null)
            {
                throw new ArgumentNullException(nameof(payments));
            }

            var paymentList = payments.ToList();

            if (paymentList.Count == 0)
            {
                throw new ArgumentException("Podaj przynajmniej jedną płatność do edycji.", nameof(payments));
            }

            if (_useDummyIdoBooking)
            {
                return new PaymentActionResponse
                {
                    Results = paymentList.Select(payment => new PaymentActionResult
                    {
                        Id = payment.Id.ToString()
                    }).ToList()
                };
            }

            var request = new PaymentEditRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new PaymentEditParams
                {
                    Payments = paymentList
                }
            };

            var response = await _idoConnect.PostAsync<PaymentEditRequest, PaymentEditResponseType>(PaymentsEditEndpoint, request, cancellationToken);

            return response?.Result;
        }

        public async Task<PaymentFormsResponse?> GetPaymentFormsAsync(CancellationToken cancellationToken = default)
        {
            if (_useDummyIdoBooking)
            {
                return new PaymentFormsResponse { Results = new List<PaymentForm>() };
            }

            var request = new PaymentFormsRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo()
            };

            var response = await _idoConnect.PostAsync<PaymentFormsRequest, PaymentFormsResponseType>(PaymentsFormsEndpoint, request, cancellationToken);

            return response?.Result;
        }

        public async Task<PaymentGetResponse?> GetPaymentsAsync(PaymentGetParams? parameters = null, PaymentGetSettings? settings = null, CancellationToken cancellationToken = default)
        {
            if (_useDummyIdoBooking && !_bookingProcessingFlag)
            {
                return new PaymentGetResponse
                {
                    Results = new List<PaymentDetails>(),
                    Page = settings?.Page ?? 1,
                    CountOnPage = 0,
                    PageAll = 1,
                    CountAll = 0
                };
            }

            var request = new PaymentGetRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Settings = settings ?? new PaymentGetSettings(),
                Params = parameters
            };

            var response = await _idoConnect.PostAsync<PaymentGetRequest, PaymentGetResponseType>(PaymentsGetEndpoint, request, cancellationToken);

            return response?.Result;
        }

        private async Task<PaymentActionResponse?> ExecutePaymentActionAsync(IEnumerable<int> paymentIds, string endpoint, CancellationToken cancellationToken)
        {
            if (paymentIds is null)
            {
                throw new ArgumentNullException(nameof(paymentIds));
            }

            var paymentList = paymentIds.Select(id => new PaymentIdentifier { Id = id }).ToList();

            if (paymentList.Count == 0)
            {
                throw new ArgumentException("Podaj przynajmniej jeden identyfikator płatności.", nameof(paymentIds));
            }

            var request = new PaymentActionRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new PaymentActionParams
                {
                    Payments = paymentList
                }
            };

            var response = await _idoConnect.PostAsync<PaymentActionRequest, PaymentActionResponseType>(endpoint, request, cancellationToken);

            return response?.Result;
        }

        private async Task<ReservationAddResponse> AddReservationsDummyAsync(List<NewReservation> reservationsList, CancellationToken cancellationToken)
        {
            var template = await _bookingDatabase.GetReservationTemplateAsync(_dummyReservationTemplateKey, _logger, cancellationToken);
            var results = new List<ReservationChangeResult>();

            foreach (var reservationRequest in reservationsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reservationId = GenerateReservationId();
                var reservation = BuildDummyReservation(reservationRequest, template, reservationId);

                await _bookingDatabase.SaveReservationJsonAsync(reservation, _logger,existingResToken: reservationRequest.RentoomResrvationID.ToString(), cancellationToken: cancellationToken);

                results.Add(new ReservationChangeResult
                {
                    Success = true,
                    ReservationId = reservationId
                });
            }

            return new ReservationAddResponse
            {
                Reservations = results
            };
        }

        private async Task<ReservationSetDiscountResponse> SetReservationsDiscountDummyAsync(List<SetReservationDiscount> reservationDiscounts, CancellationToken cancellationToken)
        {
            var appliedDiscounts = new List<SetReservationDiscount>();

            foreach (var reservationDiscount in reservationDiscounts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var reservation = await _bookingDatabase.GetReservationByIdAsync(reservationDiscount.ReservationId, _logger, cancellationToken);
                if (reservation is null)
                {
                    return new ReservationSetDiscountResponse
                    {
                        Authenticate = _idoConnect.AuthObjectIdo(),
                        Errors = new GateErrorType
                        {
                            FaultCode = 404,
                            FaultString = $"Reservation with id {reservationDiscount.ReservationId} not found in dummy storage."
                        }
                    };
                }

                if (reservation.ReservationDetails != null)
                {
                    reservation.ReservationDetails.discount = reservationDiscount.PercentValue.ToString(CultureInfo.InvariantCulture);
                }

                var updated = await _bookingDatabase.UpdateReservationJsonAsync(reservation, _logger, cancellationToken);
                if (!updated)
                {
                    return new ReservationSetDiscountResponse
                    {
                        Authenticate = _idoConnect.AuthObjectIdo(),
                        Errors = new GateErrorType
                        {
                            FaultCode = 500,
                            FaultString = $"Failed to persist discount for reservation {reservationDiscount.ReservationId} in dummy storage."
                        }
                    };
                }

                appliedDiscounts.Add(new SetReservationDiscount
                {
                    ReservationId = reservationDiscount.ReservationId,
                    PercentValue = reservationDiscount.PercentValue
                });
            }

            return new ReservationSetDiscountResponse
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Reservations = appliedDiscounts
            };
        }

        private Reservation BuildDummyReservation(NewReservation reservationRequest, Reservation? template, int reservationId)
        {
            var reservation = CloneReservation(template) ?? new Reservation
            {
                ReservationDetails = new ReservationDetails(),
                Items = new List<ReservationItem>(),
                Client = new ClientModel { Guests = new List<Guest>(), InvoiceData = new InvoiceData() }
            };

            reservation.id = reservationId;
            reservation.RentoomReservationId = reservationRequest.RentoomResrvationID;
            reservation.ReservationDetails ??= new ReservationDetails();
            reservation.Items ??= new List<ReservationItem>();
            reservation.Client ??= new ClientModel { Guests = new List<Guest>(), InvoiceData = new InvoiceData() };

            ApplyReservationDetails(reservation.ReservationDetails, reservationRequest);
            reservation.Items = BuildReservationItems(reservationRequest.Items, reservation.Items);
            ApplyClientData(reservation.Client, reservationRequest.ClientData, reservationRequest.Currency);

            return reservation;
        }

        private static void ApplyEditToReservation(Reservation reservation, EditReservation reservationEdit)
        {
            reservation.ReservationDetails ??= new ReservationDetails();
            reservation.Items ??= new List<ReservationItem>();

            var details = reservation.ReservationDetails;
            if (!string.IsNullOrWhiteSpace(reservationEdit.DateFrom))
            {
                details.dateFrom = reservationEdit.DateFrom;
            }

            if (!string.IsNullOrWhiteSpace(reservationEdit.DateTo))
            {
                details.dateTo = reservationEdit.DateTo;
            }

            if (reservationEdit.ClientId.HasValue)
            {
                details.clientId = reservationEdit.ClientId.Value.ToString(CultureInfo.InvariantCulture);
                if (reservation.Client is not null)
                {
                    reservation.Client.Id = reservationEdit.ClientId.Value;
                }
            }

            if (reservationEdit.ClientNote is not null)
            {
                details.clientNote = reservationEdit.ClientNote;
                if (reservation.Client is not null)
                {
                    reservation.Client.ClientNote = reservationEdit.ClientNote;
                }
            }

            if (reservationEdit.ExternalNote is not null)
            {
                details.externalNote = reservationEdit.ExternalNote;
            }

            if (reservationEdit.ApiNote is not null)
            {
                details.apiNote = reservationEdit.ApiNote;
            }

            if (reservationEdit.InternalNote is not null)
            {
                details.internalNote = reservationEdit.InternalNote;
            }

            details.modificationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            if (reservationEdit.Items is null)
            {
                return;
            }

            foreach (var editItem in reservationEdit.Items)
            {
                var targetItem = reservation.Items.FirstOrDefault(i => i.objectItemId == editItem.ObjectItemId);
                if (targetItem is null)
                {
                    targetItem = new ReservationItem
                    {
                        objectItemId = editItem.ObjectItemId,
                        addons = new List<ReservationAddon>()
                    };
                    reservation.Items.Add(targetItem);
                }

                ApplyEditToReservationItem(targetItem, editItem);
            }
        }

        private static void ApplyEditToReservationItem(ReservationItem targetItem, EditReservationItem editItem)
        {
            targetItem.addons ??= new List<ReservationAddon>();

            if (editItem.Price.HasValue)
            {
                targetItem.price = editItem.Price.Value;
            }

            if (editItem.PriceCorrection.HasValue)
            {
                targetItem.priceCorrection = editItem.PriceCorrection.Value;
            }

            if (editItem.Vat.HasValue)
            {
                targetItem.vat = editItem.Vat.Value;
            }

            if (editItem.NumberOfAdults.HasValue)
            {
                targetItem.numberOfAdults = editItem.NumberOfAdults.Value;
            }

            if (editItem.NumberOfSmallChildren.HasValue)
            {
                targetItem.numberOfSmallChildren = editItem.NumberOfSmallChildren.Value.ToString(CultureInfo.InvariantCulture);
            }
            else if (editItem.NumberOfBigChildren.HasValue && string.IsNullOrWhiteSpace(targetItem.numberOfSmallChildren))
            {
                targetItem.numberOfSmallChildren = editItem.NumberOfBigChildren.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (editItem.Addons is not null)
            {
                foreach (var newAddon in editItem.Addons)
                {
                    var found = targetItem.addons.Any(a =>
                        int.TryParse(a.addonId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existingAddonId)
                        && existingAddonId == newAddon.AddonId);
                    if (!found)

                        targetItem.addons.Add(new ReservationAddon
                        {
                            addonId = newAddon.AddonId.ToString(CultureInfo.InvariantCulture),
                            persons = newAddon.Persons,
                            price = newAddon.Price.ToString(CultureInfo.InvariantCulture),
                            vat = newAddon.Vat,
                            nights = newAddon.Nights,
                            quantity = newAddon.Quantity,
                            addonName = newAddon.AddonName,

                        });

                      
                }
            }
        }
        private void ApplyReservationDetails(ReservationDetails details, NewReservation reservationRequest)
        {
            details.dateFrom = reservationRequest.DateFrom;
            details.dateTo = reservationRequest.DateTo;
            details.idbDateTo = reservationRequest.DateTo;
            details.idbDateFrom = reservationRequest.DateFrom;
            if (reservationRequest.Price.HasValue)
            {
                details.price = reservationRequest.Price.Value;
            }

            if (!string.IsNullOrWhiteSpace(reservationRequest.Currency))
            {
                details.currency = reservationRequest.Currency;
            }

            if (!string.IsNullOrWhiteSpace(reservationRequest.Status))
            {
                details.status = reservationRequest.Status;
            }

            if (!string.IsNullOrWhiteSpace(reservationRequest.InternalSource))
            {
                details.internalSourceId = reservationRequest.InternalSource;
            }

            details.clientNote = reservationRequest.ClientNote ?? details.clientNote;
            details.externalNote = reservationRequest.ExternalNote ?? details.externalNote;
            details.internalNote = reservationRequest.InternalNote ?? details.internalNote;
            details.apiNote = reservationRequest.ApiNote ?? details.apiNote;
            //details.modificationDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        }

        private List<ReservationItem> BuildReservationItems(List<NewReservationItem> items, List<ReservationItem> templateItems)
        {
            var result = new List<ReservationItem>();
            for (var index = 0; index < items.Count; index++)
            {
                var requestItem = items[index];
                var templateItem = templateItems.Count > index ? templateItems[index] : new ReservationItem();
                var reservationItem = new ReservationItem
                {
                    objectItemId = requestItem.ObjectItemId,
                    price = requestItem.Price ?? templateItem.price,
                    vat = requestItem.Vat ?? templateItem.vat,
                    numberOfAdults = requestItem.NumberOfAdults ?? templateItem.numberOfAdults,
                    numberOfSmallChildren = requestItem.NumberOfBigChildren?.ToString() ?? templateItem.numberOfSmallChildren,
                    priceCorrection = templateItem.priceCorrection
                };

                var apartment = _apartmentRepository.FindApartmentByItemId(requestItem.ObjectItemId);
                var apartmentItem = apartment?.Items?.FirstOrDefault(item => item.Id == requestItem.ObjectItemId);
                if (apartment is not null)
                {
                    reservationItem.objectId = apartment.Id;
                    reservationItem.objectName = apartment.Name ?? templateItem.objectName;
                }
                else
                {
                    reservationItem.objectId = templateItem.objectId;
                    reservationItem.objectName = templateItem.objectName;
                }

                if (apartmentItem is not null)
                {
                    reservationItem.itemId = apartmentItem.Id ?? templateItem.itemId;
                    reservationItem.itemCode = apartmentItem.Code ?? templateItem.itemCode;
                }
                else
                {
                    reservationItem.itemId = templateItem.itemId;
                    reservationItem.itemCode = templateItem.itemCode;
                }

                reservationItem.addons = requestItem.Addons?.Select(addon => new ReservationAddon
                {
                    addonId = addon.AddonId.ToString(),
                    persons = addon.Persons,
                    price = addon.Price.ToString(CultureInfo.InvariantCulture),
                    vat = addon.Vat,
                    nights = addon.Nights,
                    quantity = addon.Quantity
                }).ToList() ?? templateItem.addons;

                result.Add(reservationItem);
            }

            return result;
        }

        private static void ApplyClientData(ClientModel client, ClientWithGuest? clientData, string? currency)
        {
            if (clientData is null)
            {
                return;
            }

            client.FirstName = clientData.FirstName;
            client.LastName = clientData.LastName;
            client.Email = clientData.Email;
            client.Phone = clientData.Phone;
            client.Street = clientData.Street;
            client.Zipcode = clientData.Zipcode;
            client.City = clientData.City;
            client.CountryCode = clientData.CountryCode;
            client.Language = string.IsNullOrWhiteSpace(clientData.Language) ? client.Language : clientData.Language;
            client.Currency = string.IsNullOrWhiteSpace(clientData.Currency) ? (currency ?? client.Currency) : clientData.Currency;

            if (clientData.Guests is not null && clientData.Guests.Count > 0)
            {
                client.Guests = clientData.Guests.Select(guest => new Guest
                {
                    FirstName = guest.FirstName,
                    LastName = guest.LastName,
                    City = guest.City,
                    CountryCode = guest.CountryCode,
                    Email = guest.Email,
                    Language = guest.Language,
                    Phone = guest.Phone,
                    Street = guest.Street,
                    Zipcode = guest.Zipcode
                }).ToList();
            }

            if (clientData.InvoiceData is not null)
            {
                client.InvoiceData = new InvoiceData
                {
                    FirstName = clientData.InvoiceData.FirstName,
                    LastName = clientData.InvoiceData.LastName,
                    Street = clientData.InvoiceData.Street,
                    Zipcode = clientData.InvoiceData.Zipcode,
                    City = clientData.InvoiceData.City,
                    CountryCode = clientData.InvoiceData.CountryCode
                };
            }
        }

        private static Reservation? CloneReservation(Reservation? template)
        {
            if (template is null)
            {
                return null;
            }

            var serialized = JsonConvert.SerializeObject(template);
            return JsonConvert.DeserializeObject<Reservation>(serialized);
        }

        private static int GenerateReservationId()
        {
            return RandomNumberGenerator.GetInt32(100000, 99999999);
        }

        private static PaymentActionResponse BuildDummyPaymentActionResponse(IEnumerable<int> paymentIds)
        {
            var paymentList = paymentIds?.ToList() ?? new List<int>();
            return new PaymentActionResponse
            {
                Results = paymentList.Select(id => new PaymentActionResult
                {
                    Id = id.ToString()
                }).ToList()
            };
        }

    }
}


