using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;


TokenCredential credential = new DefaultAzureCredential();

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<IdoSellService>();
builder.Services.AddSingleton<BookingDatabase>();
builder.Services.AddSingleton<IdoLocksService, IdoLocksService>();
builder.Services.AddSingleton<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
builder.Services.AddSingleton<IClientService, ClientService>();
builder.Services.AddSingleton<IIdoApartmentService, IdoApartmentService>();
builder.Services.AddSingleton<IIdoBookingConnectService, IdoBookingConnectService>();
builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
builder.Services.AddSingleton<ApartmentRepository>();
builder.Services.AddSingleton<FiltersRepository>();
builder.Services.AddSingleton<IIdoOfferService,IdoOfferService>();
builder.Services.AddSingleton<IRentoomOfferService, RentoomOfferService>();
var cosendpoint = builder.Configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT");
//cosendpoint = builder.Configuration["AZURE_COSMOS_ENDPOINT"];
if (string.IsNullOrEmpty(cosendpoint))
{
    throw new InvalidOperationException("AZURE_COSMOS_ENDPOINT configuration is missing.");
}


var cosmosClient = new CosmosClient(cosendpoint, new CosmosClientOptions()
{
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    },
    AllowBulkExecution = false,
    MaxRetryAttemptsOnRateLimitedRequests = 12,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromMinutes(1)

});

builder.Services.AddSingleton(cosmosClient);

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver()
};


builder.Build().Run();

