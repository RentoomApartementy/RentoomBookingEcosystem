using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IClientService
    {
        Task<ClientGetResponse?> GetClientsAsync(ClientGetParams? parameters = null, ResultRequestPaging? settings = null, CancellationToken cancellationToken = default);
        Task<ClientGetResponse?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<ClientAddResponse?> AddClientsAsync(IEnumerable<ClientAddRequestClient> clients, CancellationToken cancellationToken = default);
        Task<ClientAddResponse?> AddClientAsync(ClientAddRequestClient client, CancellationToken cancellationToken = default);
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

        public async Task<ClientGetResponse?> GetClientsAsync(ClientGetParams? parameters = null, ResultRequestPaging? settings = null, CancellationToken cancellationToken = default)
        {
            var request = new ClientGetRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Settings = settings,
                Params = parameters
            };

            var ret = await _idoConnect.PostAsync<ClientGetRequest, ClientGetResponseType>(ClientsGetEndpoint, request, cancellationToken);
            return ret?.Result;
        }

        public async Task<ClientGetResponse?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            if (clientId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clientId));
            }

            var ret  = await GetClientsAsync(new ClientGetParams { Id = clientId }, null, cancellationToken);
            return ret;

        }

        public async Task<ClientAddResponse?> AddClientsAsync(IEnumerable<ClientAddRequestClient> clients, CancellationToken cancellationToken = default)
        {
            if (clients is null)
            {
                throw new ArgumentNullException(nameof(clients));
            }

            var clientsList = clients.ToList();
            if (clientsList.Count == 0)
            {
                throw new ArgumentException("At least one client must be provided.", nameof(clients));
            }

            var request = new ClientAddRequest
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Params = new ClientAddParams
                {
                    Clients = clientsList
                }
            };

            var response = await _idoConnect.PostAsync<ClientAddRequest, ClientAddResponseType>(ClientsAddEndpoint, request, cancellationToken);
            return response?.Result;
        }

        public Task<ClientAddResponse?> AddClientAsync(ClientAddRequestClient client, CancellationToken cancellationToken = default)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            return AddClientsAsync([client], cancellationToken);
        }

    }
}
