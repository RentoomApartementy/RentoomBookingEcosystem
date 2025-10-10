using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IClientService
    {
        Task<ClientGetResponseType?> GetClientsAsync(ClientGetParams? parameters = null, ResultRequestPaging? settings = null, CancellationToken cancellationToken = default);
    }

    public class ClientService : IClientService
    {
        private const string ClientsGetEndpoint = "clients/get/34/json";
        private const string ClientsAddEndpoint = "clients/add/34/json";

       // private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ClientService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;

        public ClientService(IIdoBookingConnectService idoConnect ,ILogger<ClientService> logger)
        {
           // _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idoConnect = idoConnect;
            
        }

        public Task<ClientGetResponseType?> GetClientsAsync(ClientGetParams? parameters = null, ResultRequestPaging? settings = null, CancellationToken cancellationToken = default)
        {
            var request = new ClientGetRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Settings = settings,
                Params = parameters
            };
            return _idoConnect.PostAsync<ClientGetRequest, ClientGetResponseType>(ClientsGetEndpoint, request, cancellationToken);
        }

    }
}
