using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IApartmentService
    {
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default);
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(CancellationToken ct = default);
        Task<ApartmentObject?> GetApartmentByIdAsync(int objectId);
    }
    public class ApartmentService: IApartmentService
    {

        //private const string ApartmentsGetEndpoint = "clients/get/34/json";
        private const string ApartmentsLocationGetEndpoint = "objects/getLocation/34/json";
        private const string PublicParametersGetEndpoint = "public/parameters/34/json";
        private const string ApartemntsGetEndpoint = "objects/getAll/34/json";

        // private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IApartmentService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;

        private readonly ApartmentRepository _apartmentRepository;

        public ApartmentService(IIdoBookingConnectService idoConnect, ILogger<IApartmentService> logger, ApartmentRepository apartmentRepository)
        {
           // _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idoConnect = idoConnect;
            _apartmentRepository = apartmentRepository;

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

            var resp =  await _idoConnect.PostAsync<GetObjectLocationRequestType, GetObjectLocationResult>(ApartmentsLocationGetEndpoint, request, ct);
            var x = await GetPublicObjectLocationsAsync(ct);
            return resp.Result;
        }


        public async Task<List<LocalizationItem>?> GetPublicObjectLocationsAsync(CancellationToken ct = default)
        {

            PublicParametersResult? resp = await _idoConnect.PostAsync<object, PublicParametersResult>(PublicParametersGetEndpoint,null, ct);

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

           apartments?.ForEach(a => a.ObjectLocation = Objlocs.ObjectLocations?.FirstOrDefault(l => l.ObjectId.ToString() == a.Id?.Trim()));

            await _apartmentRepository.SaveApartmentsAsync(apartments, _logger, ct);
            
            return apartments; 
        }

        public async Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default)
        {
            List<ApartmentObject> retList = new();

            int currentPage = 1;
            int pageAll = 1;

            do { 
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

public  async Task<ApartmentObject?> GetApartmentByIdAsync(int objectId)
        {
            return await _apartmentRepository.FindApartmentInCosmosDb(objectId);
        }


    }
}
