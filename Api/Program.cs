using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;


TokenCredential credential = new DefaultAzureCredential();

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();
builder.Services.AddHttpClient();

using var tempLoggerFactory = LoggerFactory.Create(lb =>
{
    lb.AddConfiguration(builder.Configuration.GetSection("Logging"));
    lb.AddConsole();
    lb.AddDebug();
});
var tempLogger = tempLoggerFactory.CreateLogger("DatabaseInit");

var postgresConnectionString = PostgresConnectionStringProvider
    .GetPostgresConnectionStringAsync(builder.Configuration, builder.Environment.IsDevelopment(), tempLogger)
    .Result;

builder.Services.AddDbContext<PostgresBookingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));


builder.Services.AddScoped<PostgresBookingDatabase>();
builder.Services.AddScoped<IdoSellService>();
builder.Services.AddScoped<BookingDatabase>();
builder.Services.AddScoped<PostgresBookingDatabase>();
builder.Services.AddScoped<IdoLocksService, IdoLocksService>();
builder.Services.AddScoped<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IIdoApartmentService, IdoApartmentService>();
builder.Services.AddScoped<IIdoBookingConnectService, IdoBookingConnectService>();
builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
builder.Services.AddScoped<ApartmentRepository>();
builder.Services.AddScoped<FiltersRepository>();
builder.Services.AddScoped<IIdoOfferService,IdoOfferService>();
builder.Services.AddScoped<IRentoomOfferService, RentoomOfferService>();


var cosendpoint = CosmosEndpointProvider.GetCosmosEndpointAsync(builder.Configuration, builder.Environment.IsDevelopment(), tempLogger).Result;

//builder.Configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT");

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


//POSTGRESS

if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException("PostgreSQL connection string configuration is missing.");
}






JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver()
};


builder.Build().Run();

