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
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Services;
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

builder.Services.AddDbContextFactory<PostgresBookingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddDbContext<QrMaintRappDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("RentoomDbConnectionString")));

builder.Services.AddScoped<RappQrMaintService>();
builder.Services.AddScoped<PostgresBookingDatabase>();
builder.Services.AddScoped<IdoSellService>();
builder.Services.AddScoped<IdoLocksService, IdoLocksService>();
builder.Services.AddScoped<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IIdoApartmentService, IdoApartmentService>();
builder.Services.AddScoped<IIdoBookingConnectService, IdoBookingConnectService>();
builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
builder.Services.AddScoped<ApartmentRepository>();
builder.Services.AddScoped<FiltersRepository>();
builder.Services.AddScoped<TermsRepository>();
builder.Services.AddScoped<RegistrationCardRepository>();
builder.Services.AddScoped<IIdoOfferService,IdoOfferService>();
builder.Services.AddScoped<IRentoomOfferService, RentoomOfferService>();
builder.Services.AddScoped<BitrixService>();

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

