using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;

using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IIdoApartmentService
    {
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default);
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(CancellationToken ct = default);
        Task<List<ObjectMedium>?> GetObjectMediaFromIdoSellAsync(int objectId, CancellationToken ct = default);
        Task<List<ObjectDescription>?> GetObjectDescriptionsAsync(int objectId, string? language = null, CancellationToken ct = default);
        Task<List<ObjectAmenity>?> GetObjectAmenitiesAsync(int objectId, CancellationToken ct = default);
        Task<List<ApartmentObject>> SyncApartmentsAndAmenitiesAsync(CancellationToken ct = default);
        Task<List<ApartmentObject>> SaveAllApartmentsToPostgresAsync(CancellationToken ct = default);

    }
    public class IdoApartmentService : IIdoApartmentService
    {

        //private const string ApartmentsGetEndpoint = "clients/get/34/json";
        private const string PublicParametersGetEndpoint = "public/parameters/34/json";
        private const string ApartmentsLocationGetEndpoint = "objects/getLocation/34/json";
        private const string ApartemntsGetEndpoint = "objects/getAll/34/json";
        private const string ObjectMediaGetEndpoint = "objects/getMedia/34/json";
        private const string ApartmentAmenitiesGetEndpoint = "objects/getAmenities/34/json";
        private const string ObjectDescriptionsGetEndpoint = "objects/getDescriptions/34/json";

        // private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IIdoApartmentService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;

        private readonly ApartmentRepository _apartmentRepository;
        private readonly PostgresBookingDatabase _postgresBookingDatabase;

        public IdoApartmentService(IIdoBookingConnectService idoConnect, ILogger<IdoApartmentService> logger, ApartmentRepository apartmentRepository, PostgresBookingDatabase postgresBookingDatabase)
        {
            // _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idoConnect = idoConnect;
            _apartmentRepository = apartmentRepository;
            _postgresBookingDatabase = postgresBookingDatabase ?? throw new ArgumentNullException(nameof(postgresBookingDatabase));
        }


        public async Task<GetObjectLocationResponseType> GetObjectLocationsAsync(
           ParamsSearchObjectLocationType? parameters = null,
           CancellationToken ct = default)
        {

            var request = new GetObjectLocationRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ParamsSearchObjectLocation = parameters
            };

            var resp = await _idoConnect.PostAsync<GetObjectLocationRequestType, GetObjectLocationResult>(ApartmentsLocationGetEndpoint, request, ct);
            var x = await GetPublicObjectLocationsAsync(ct);
            return resp.Result;
        }


        public async Task<List<LocalizationItem>?> GetPublicObjectLocationsAsync(CancellationToken ct = default)
        {

            PublicParametersResult? resp = await _idoConnect.PostAsync<object, PublicParametersResult>(PublicParametersGetEndpoint, null, ct);

            return resp?.Result.Locations;
        }

        public async Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(CancellationToken ct = default)
        {


            List<LocalizationItem> locs = await GetPublicObjectLocationsAsync(ct);

            List<ApartmentObject> apartments = await GetAllApartmentsFromIdoSellAsync(ct);

            var _params = IdoBookingBaseHelper.BuildObjectLocationParams(apartments);

            GetObjectLocationResponseType Objlocs = await GetObjectLocationsAsync(_params, ct);
            //Objlocs.ObjectLocations

            Objlocs.ObjectLocations.ForEach(a => a.LocalizationItem = locs?.FirstOrDefault(loc => loc.Id == a.LocationId));

            apartments?.ForEach(a => a.ObjectLocation = Objlocs.ObjectLocations?.FirstOrDefault(l => l.ObjectId == a.Id));

            await _apartmentRepository.SaveApartmentsAsync(apartments, _logger, ct);

            return apartments;
        }

        public async Task<List<ApartmentObject>> SaveAllApartmentsToPostgresAsync(CancellationToken ct = default)
        {
            List<LocalizationItem> locs = await GetPublicObjectLocationsAsync(ct);

            List<ApartmentObject> apartments = await GetAllApartmentsFromIdoSellAsync(ct);

            var parameters = IdoBookingBaseHelper.BuildObjectLocationParams(apartments);

            GetObjectLocationResponseType objLocs = await GetObjectLocationsAsync(parameters, ct);

            objLocs.ObjectLocations.ForEach(a => a.LocalizationItem = locs?.FirstOrDefault(loc => loc.Id == a.LocationId));

            apartments?.ForEach(a => a.ObjectLocation = objLocs.ObjectLocations?.FirstOrDefault(l => l.ObjectId == a.Id));

            await _postgresBookingDatabase.SaveApartmentsAsync(apartments, _logger, ct);

            return apartments;
        }




        public async Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default)
        {
            List<ApartmentObject> retList = new();

            int currentPage = 1;
            int pageAll = 1;

            do
            {
                var ret = await GetApartmentsByPageFromIdoSellAsync(currentPage);

                pageAll = ret.Result?.Result?.PageAll ?? 0;

                if (pageAll == 0)
                {
                    _logger.LogWarning("apiResponse.Result or apiResponse.Result.Result is null, or PageAll is 0. Ending sync.");
                    break;
                }

                retList.AddRange(ret.Result.Objects);

                _logger.LogInformation($"Number of apartments fetched so far: {retList.Count}");

                currentPage++;

            } while (currentPage <= pageAll);
            return retList;
        }


        public async Task<ApartmentResponseType> GetApartmentsByPageFromIdoSellAsync(int page, CancellationToken ct = default)
        {

            var request = new ApartmentRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = new ResultRequestPaging { Page = page, Number = 100 },
            };

            ApartmentResponseType? resp = await _idoConnect.PostAsync<ApartmentRequestType, ApartmentResponseType>(ApartemntsGetEndpoint, request, ct);
            return resp;



        }


        public async Task<List<ObjectMedium>?> GetObjectMediaFromIdoSellAsync(int objectId, CancellationToken ct = default)
        {
            var request = new ObjectMediaRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectId = objectId
            };
            var ret =  await _idoConnect.PostAsync<ObjectMediaRequestType, ObjectMediaResponseType>(ObjectMediaGetEndpoint, request, ct);
            return ret?.Result.ObjectMedia;
        }

        public async Task<List<ObjectDescription>?> GetObjectDescriptionsAsync(int objectId, string? language = null, CancellationToken ct = default)
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

            var ret = await _idoConnect.PostAsync<ObjectDescriptionsRequestType, ObjectDescriptionsResponseType>(ObjectDescriptionsGetEndpoint, request, ct);
            return ret?.Result.ObjectDescriptions;
        }


        public async Task<List<ObjectAmenity>?> GetObjectAmenitiesAsync(int objectId, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("Fetching amenities for object {ObjectId}", objectId);

            var request = new ObjectAmenitiesRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectId = objectId
            };
            var ret = await _idoConnect.PostAsync<ObjectAmenitiesRequestType, ObjectAmenitiesResponseType>(ApartmentAmenitiesGetEndpoint, request, cancellationToken);

            /*
               if (!response.IsSuccessStatusCode)
               {
                   _logger.LogError("Failed to fetch media for object {ObjectId}. StatusCode: {StatusCode}. Content: {Content}", objectId, response.StatusCode, responseContent);
                   response.EnsureSuccessStatusCode();
               }
            */
            // ObjectAmenitiesResponseType ret = JsonConvert.DeserializeObject<ObjectAmenitiesResponseType>(responseContent);
            return ret?.Result.ObjectAmenities;
        }


        public async Task<List<ApartmentObject>> SyncApartmentsAndAmenitiesAsync(CancellationToken ct = default)
        {
            var apartments = await SaveAllApartmentsToPostgresAsync(ct);

            var amenitiesDocuments = new List<ApartmentAmenitiesDocument>(apartments.Count);

            foreach (var apartment in apartments)
            {
                ct.ThrowIfCancellationRequested();

                var amenities = await GetObjectAmenitiesAsync(apartment.Id, ct) ?? new List<ObjectAmenity>();

                var document = new ApartmentAmenitiesDocument
                {
                    Id = apartment.Id,
                    ApartmentId = apartment.Id,
                  //  Apartment = apartment,
                    Amenities = amenities
                };

                amenitiesDocuments.Add(document);
            }

            _logger.LogInformation("Retrieved amenities for {Count} apartments.", amenitiesDocuments.Count);

            await _apartmentRepository.SaveApartmentAmenitiesAsync(amenitiesDocuments, _logger, ct);

            return apartments;// amenitiesDocuments;
        }

    }
}
