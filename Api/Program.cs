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
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Payments;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;


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
    .GetPostgresConnectionString(builder.Configuration, "POSTGRES_RENTOOM_BOOKING_DB_LOCAL", builder.Environment.IsDevelopment(), tempLogger);
  

builder.Services.AddDbContextFactory<PostgresBookingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));


var rentoomAppConnectionString = PostgresConnectionStringProvider
    .GetPostgresConnectionString(builder.Configuration, "RentoomDbConnectionString", builder.Environment.IsDevelopment(), tempLogger);
  

builder.Services.AddDbContextFactory<RappPartnersDBContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));


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
builder.Services.AddScoped<IUpsellCatalogService, UpsellCatalogService>();
builder.Services.AddScoped<IUpsellOrderStore, UpsellOrderStore>();

//Upselle
builder.Services.AddScoped<IUpsellOrderWorkflowService, UpsellOrderWorkflowService>();
builder.Services.AddScoped<IUpsellPurchasedSummaryService, UpsellPurchasedSummaryService>();

//Vouchery do upselli
builder.Services.AddScoped<IUpsellVoucherProvisioningService, UpsellVoucherProvisioningService>();
builder.Services.AddScoped<IUpsellVoucherCodeGenerator, UpsellVoucherCodeGenerator>();
//builder.Services.AddScoped<IUpsellVoucherQueryService, UpsellVoucherQueryService>();
builder.Services.AddScoped<IUpsellVoucherRedeemService, UpsellVoucherRedeemService>();


builder.Services.AddScoped<BitrixService>();

//Customer Terms
builder.Services.AddScoped<CustomerTermsRepository>();
builder.Services.AddScoped<CustomerTermsService>();

//TPAY

bool UseDevelopmentSettingsOnProd = true;
var TpaySection = UseDevelopmentSettingsOnProd ?builder.Configuration.GetSection("TpayDev"): builder.Configuration.GetSection("Tpay");

builder.Services.Configure<TpaySettings>(TpaySection);

builder.Services.AddScoped<IReservationStore, ReservationStore>();
builder.Services.AddScoped<IReservationWorkflowService, ReservationWorkflowService>();
builder.Services.AddScoped<IPaymentFlowHandler, ReservationPaymentFlowHandler>();
builder.Services.AddScoped<IPaymentFlowHandler, UpsellPaymentFlowHandler>();
builder.Services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
builder.Services.AddScoped<ITpayNotificationValidator, TpayNotificationValidator>();
builder.Services.AddScoped<ITpayGateway, TpayOpenApiGateway>();
builder.Services.AddSingleton<TpayClient>();

builder.Services.AddHttpClient("Tpay", (sp, http) =>
{
    var settings = sp.GetRequiredService<IOptions<TpaySettings>>().Value;

    if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        throw new InvalidOperationException("Tpay:ApiBaseUrl is missing.");

    http.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
    http.DefaultRequestHeaders.Accept.Clear();
    http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});



builder.Services.AddScoped<ITpayClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Tpay");
    var opt = sp.GetRequiredService<IOptions<TpaySettings>>();
    var logger = sp.GetRequiredService<ILogger<TpayClient>>();

    logger.LogInformation("Creating TpayClient with ApiBaseUrl: {ApiBaseUrl}", opt.Value.ApiBaseUrl);
    return new TpayClient(http, opt, logger);
});


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
