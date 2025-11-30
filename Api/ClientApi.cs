using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Client;
using RentoomBooking.SharedClasses.Models.IdoBooking.Rentoom;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System.Net;

namespace RentoomBooking.Api;

public class ClientApi
{

    private readonly ILogger<ClientApi> _logger;
    //  private readonly ClientRepository _ClientRepository;
    private readonly IClientService _ClientService;
    public ClientApi(ILogger<ClientApi> logger, IClientService ClientService)
    {

        _logger = logger;
        //_ClientRepository = ClientRepository;
        _ClientService = ClientService;
    }

    [Function("GetClients")]
    public async Task<HttpResponseData> GetClients(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "clients/get")] HttpRequestData req)
    {

        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();

        try
        {
            ClientGetParams? parameters = null;
            ResultRequestPaging? settings = null;

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(requestBody))
            {
                try
                {
                    var payload = JsonConvert.DeserializeObject<ClientGetRequestPayloadInternal>(requestBody);

                    if (payload?.Params != null || payload?.Settings != null)
                    {
                        parameters = payload.Params;
                        settings = payload.Settings;
                    }
                    else
                    {
                        parameters = JsonConvert.DeserializeObject<ClientGetParams>(requestBody);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Invalid client query payload.");
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid JSON payload.");
                    return response;
                }
            }

            var result = await _ClientService.GetClientsAsync(parameters, settings, cancellationToken);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get clients.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }
    [Function("GetClientById")]
    public async Task<HttpResponseData> GetClientById(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/getbyId/{id:int?}")] HttpRequestData req,
    int? id)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();

        if (!id.HasValue)
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Client Id not provided");
            return response;
        }
        try
        {
            var result = await _ClientService.GetClientByIdAsync(id.Value);

            if (result?.Clients == null || result.Clients.Count == 0)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync($"Client with id {id} not found.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get client with id {id}.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }

    [Function("GetClientByEmail")]
    public async Task<HttpResponseData> GetClientByEmail(
[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clients/getbyEmail/{email}")] HttpRequestData req,
string email)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();

        if (string.IsNullOrEmpty(email))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync("Client email not provided");
            return response;
        }
        try
        {
            var result = await _ClientService.GetClientByEmailAsync(email);

            if (result?.Clients == null || result.Clients.Count == 0)
            {
                response.StatusCode = HttpStatusCode.NotFound;
                await response.WriteStringAsync($"Client with email {email} not found.");
                return response;
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get client with email {email}.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }


    [Function("AddClient")]
    public async Task<HttpResponseData> AddClient(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clients/add")] HttpRequestData req)
    {
        var cancellationToken = req.FunctionContext.CancellationToken;
        var response = req.CreateResponse();

        try
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Request body is empty.");
                return response;
            }

            ClientAddRequestClient? client;
            try
            {
                client = JsonConvert.DeserializeObject<ClientAddRequestClient>(requestBody);
                if (client == null)
                {
                    response.StatusCode = HttpStatusCode.BadRequest;
                    await response.WriteStringAsync("Invalid client payload.");
                    return response;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid client add payload.");
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.");
                return response;
            }

            var result = await _ClientService.AddClientAsync(client, cancellationToken);

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result));
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add client.");
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.");
            return response;
        }
    }
}
