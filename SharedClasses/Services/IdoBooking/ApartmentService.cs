using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IApartmentService
    {
        Task<GetObjectLocationResponseType?> GetObjectLocationsAsync(
           ParamsSearchObjectLocationType? parameters = null,
           CancellationToken ct = default);
    }
    public class ApartmentService: IApartmentService
    {

        //private const string ApartmentsGetEndpoint = "clients/get/34/json";
        private const string ApartmentsLocationGetEndpoint = "objects/getLocation/34/json";


       // private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IApartmentService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;


        public ApartmentService(IIdoBookingConnectService idoConnect, ILogger<IApartmentService> logger)
        {
           // _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idoConnect = idoConnect;

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

            return resp.Result;
        }


    }
}
