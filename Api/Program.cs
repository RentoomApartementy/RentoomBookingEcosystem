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
using RentoomBooking.Api.Chat;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Data;
using RentoomBooking.ChatAI.Repositories;
using RentoomBooking.ChatAI.Services;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Events.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Integrations.TTLock;
using RentoomBooking.SharedClasses.Integrations.TTLock.Models;
using RentoomBooking.SharedClasses.Integrations.TTLock.Services;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.Storage;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.BookingCom;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Cookies;
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
builder.Services.AddHttpClient("LinkPreview")
    .ConfigureHttpClient(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(15);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; RentoomBot/1.0)");
    });
builder.Services.AddMemoryCache();

using var tempLoggerFactory = LoggerFactory.Create(lb =>
{
    lb.AddConfiguration(builder.Configuration.GetSection("Logging"));
    lb.AddConsole();
    lb.AddDebug();
});
var tempLogger = tempLoggerFactory.CreateLogger("DatabaseInit");

var postgresConnectionString = PostgresConnectionStringProvider
    .GetPostgresConnectionString(builder.Configuration, "POSTGRES_RENTOOM_BOOKING_DB_LOCAL", builder.Environment.IsDevelopment(), tempLogger);

var rentoomAppConnectionString = PostgresConnectionStringProvider
    .GetPostgresConnectionString(builder.Configuration, "RentoomDbConnectionString", builder.Environment.IsDevelopment(), tempLogger);

if (string.IsNullOrWhiteSpace(postgresConnectionString))
{
    throw new InvalidOperationException("PostgreSQL connection string configuration is missing.");
}

if (string.IsNullOrWhiteSpace(rentoomAppConnectionString))
{
    throw new InvalidOperationException("RentoomAppDb connection string configuration is missing.");
}

builder.Services.AddDbContextFactory<PostgresBookingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddDbContextFactory<ChatAIDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddDbContextFactory<RentoomBooking.LiveChat.Data.LiveChatDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services.AddDbContext<QrMaintRappDbContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));

builder.Services.AddDbContextFactory<RappPartnersDBContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));

builder.Services.AddDbContextFactory<RappInstructionsDbContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));

builder.Services.AddDbContextFactory<RappEventsDbContext>(options =>
    options.UseNpgsql(rentoomAppConnectionString));


builder.Services.AddScoped<PostgresBookingDatabase>();
builder.Services.AddScoped<IdoSellService>();
builder.Services.AddScoped<IIdoLocksService, IdoLocksService>();
builder.Services.AddScoped<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IIdoApartmentService, IdoApartmentService>();
builder.Services.AddScoped<IIdoBookingConnectService, IdoBookingConnectService>();
builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
builder.Services.AddScoped<ApartmentRepository>();
builder.Services.AddScoped<FiltersRepository>();
builder.Services.AddScoped<TermsRepository>();
builder.Services.AddScoped<RappQrMaintService>();
builder.Services.AddScoped<RegistrationCardRepository>();
builder.Services.AddScoped<IIdoOfferService,IdoOfferService>();
builder.Services.AddScoped<IRentoomOfferService, RentoomOfferService>();
builder.Services.AddScoped<IAvailabilityFinderService2, AvailabilityFinderService2>();
builder.Services.AddScoped<IUpsellCatalogService, UpsellCatalogService>();
builder.Services.AddScoped<IUpsellOrderStore, UpsellOrderStore>();

var ttlockSection = builder.Configuration.GetSection("TTLOCK");
builder.Services.Configure<TTLockSettings>(ttlockSection);

builder.Services.AddHttpClient<TTLockService>();
builder.Services.AddScoped<ITTLockPasscodeAppService, TTLockPasscodeAppService>();

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
builder.Services.AddScoped<CookieConsentRepository>();
builder.Services.AddScoped<CookieConsentService>();

//arrival instructions
builder.Services.AddScoped<ArrivalInstructionsService>();
builder.Services.AddSingleton<LockInstructionsService>();

// TPAY
var TpaySection = builder.Configuration.GetSection("Tpay");
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<StorageOptions>("InstructionsStorage", builder.Configuration.GetSection("InstructionsStorage"));

builder.Services.Configure<TpaySettings>(TpaySection);

builder.Services.AddScoped<IReservationStore, ReservationStore>();
builder.Services.AddScoped<ReservationWorkflowService>();
builder.Services.AddScoped<IReservationWorkflowService>(sp => sp.GetRequiredService<ReservationWorkflowService>());
builder.Services.AddScoped<IReservationWorkflowSyncOperations>(sp => sp.GetRequiredService<ReservationWorkflowService>());
builder.Services.AddScoped<IReservationSyncService, ReservationSyncService>();
builder.Services.AddScoped<IPaymentFlowHandler, ReservationPaymentFlowHandler>();
builder.Services.AddScoped<IPaymentFlowHandler, UpsellPaymentFlowHandler>();
builder.Services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
builder.Services.AddScoped<ITpayNotificationValidator, TpayNotificationValidator>();
builder.Services.AddScoped<ITpayGateway, TpayOpenApiGateway>();
builder.Services.AddScoped<IBookingComLogStore, BookingComLogStore>();
builder.Services.AddScoped<IBookingComIncomingEmailProcessor, BookingComIncomingEmailProcessor>();
builder.Services.AddScoped<IBookingComBackfillImportBuilder, BookingComBackfillImportBuilder>();
builder.Services.AddScoped<IBookingComReservationWorkflowService, BookingComReservationWorkflowService>();

builder.Services.AddOptions<StaywellChatOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        configuration.GetSection(StaywellChatOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.Endpoint) || string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.DeploymentName))
        {
            var azureSection = configuration.GetSection("StaywellChat");
            if (!azureSection.Exists())
            {
                azureSection = configuration.GetSection("AzureOpenAi_general");
            }

            if (string.IsNullOrWhiteSpace(options.Endpoint))
            {
                options.Endpoint = azureSection["Endpoint"] ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                options.ApiKey = azureSection["ApiKey"] ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.DeploymentName))
            {
                options.DeploymentName = azureSection["DeploymentName"] ?? string.Empty;
            }
        }

        if (options.MaxMessageLength < 100)
        {
            options.MaxMessageLength = 2000;
        }

        if (options.MaxHistoryMessages < 1)
        {
            options.MaxHistoryMessages = 15;
        }

        if (options.MaxRequestsPerMinute < 1)
        {
            options.MaxRequestsPerMinute = 12;
        }

        if (options.StreamingTimeoutSeconds < 10)
        {
            options.StreamingTimeoutSeconds = 90;
        }
    });

builder.Services.AddOptions<StaywellAgentChatOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        configuration.GetSection(StaywellAgentChatOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.AgentName))
        {
            options.AgentName = "staywell-events-mvp";
        }

        if (string.IsNullOrWhiteSpace(options.TokenScope))
        {
            options.TokenScope = "https://ai.azure.com/.default";
        }

        if (options.MaxMessageLength < 100)
        {
            options.MaxMessageLength = 2000;
        }

        if (options.MaxHistoryMessages < 1)
        {
            options.MaxHistoryMessages = 15;
        }

        if (options.MaxRequestsPerMinute < 1)
        {
            options.MaxRequestsPerMinute = 12;
        }

        if (options.StreamingTimeoutSeconds < 10)
        {
            options.StreamingTimeoutSeconds = 90;
        }
    });

builder.Services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IStaywellChatClient, AzureFoundryStaywellChatClient>();
builder.Services.AddScoped<IStaywellAgentChatClient, FoundryAgentStaywellChatClient>();
builder.Services.AddScoped<IPromptBuilder, StaywellPromptBuilder>();
builder.Services.AddScoped<IReservationContextProvider, StaywellReservationContextProvider>();
builder.Services.AddScoped<IStaywellChatService, StaywellChatService>();
builder.Services.AddScoped<IStaywellAgentChatService, StaywellAgentChatService>();
builder.Services.AddSingleton<IChatRateLimiter, MemoryChatRateLimiter>();

// LiveChat
builder.Services.AddOptions<RentoomBooking.SharedClasses.Configuration.BitrixLiveChatOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.Domain = RentoomBooking.SharedClasses.Configuration.BitrixConfiguration.GetDomain(configuration);
        options.ConnectorId = RentoomBooking.SharedClasses.Configuration.BitrixLiveChatConfiguration.GetConnectorId(configuration);
        options.OpenLineId = RentoomBooking.SharedClasses.Configuration.BitrixLiveChatConfiguration.GetOpenLineId(configuration);
        options.OAuthClientId = configuration["BitrixLiveChat:OAuthClientId"] ?? string.Empty;
        options.OAuthClientSecret = configuration["BitrixLiveChat:OAuthClientSecret"] ?? string.Empty;
        options.OAuthRefreshToken = configuration["BitrixLiveChat:OAuthRefreshToken"];
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<RentoomBooking.LiveChat.ILiveChatRateLimiter, RentoomBooking.LiveChat.MemoryLiveChatRateLimiter>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.ILiveChatSseSubscriptions, RentoomBooking.LiveChat.LiveChatSseSubscriptions>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Bitrix.IBitrixOAuthService, RentoomBooking.LiveChat.Bitrix.BitrixOAuthService>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Bitrix.IBitrixMessageSender, RentoomBooking.LiveChat.Bitrix.BitrixMessageSender>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Bitrix.IBitrixUserService, RentoomBooking.LiveChat.Bitrix.BitrixUserService>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Bitrix.IBitrixWebhookService, RentoomBooking.LiveChat.Bitrix.BitrixWebhookService>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Bitrix.IBitrixLandingService, RentoomBooking.LiveChat.Bitrix.BitrixLandingService>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.IBitrixRestClient, RentoomBooking.LiveChat.Bitrix.BitrixRestClient>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Repositories.ILiveChatSessionRepository, RentoomBooking.LiveChat.Repositories.LiveChatSessionRepository>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Repositories.ILiveChatMessageRepository, RentoomBooking.LiveChat.Repositories.LiveChatMessageRepository>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.Repositories.IBitrixPortalRepository, RentoomBooking.LiveChat.Repositories.BitrixPortalRepository>();
builder.Services.AddSingleton<RentoomBooking.LiveChat.BitrixLiveChatService>();

// Azure Translator for LiveChat
builder.Services.AddOptions<RentoomBooking.LiveChat.Services.AzureTranslatorOptions>()
    .Configure<IConfiguration>((options, configuration) =>
    {
        options.Key = configuration["AzureTranslator:Key"] ?? throw new InvalidOperationException("AzureTranslator:Key is missing");
        options.Endpoint = configuration["AzureTranslator:Endpoint"] ?? throw new InvalidOperationException("AzureTranslator:Endpoint is missing");
        options.DefaultSourceLanguage = configuration["AzureTranslator:DefaultSourceLanguage"] ?? "auto";
        options.DefaultTargetLanguage = configuration["AzureTranslator:DefaultTargetLanguage"] ?? "pl";
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<RentoomBooking.LiveChat.Services.AzureTranslatorOptions>>().Value;
    return new Azure.AI.Translation.Text.TextTranslationClient(
        new Azure.AzureKeyCredential(options.Key),
        options.Endpoint);
});

builder.Services.AddScoped<RentoomBooking.LiveChat.Services.ITranslationService, RentoomBooking.LiveChat.Services.AzureTranslatorService>();

builder.Services.AddScoped<IEventReadRepository, EventReadRepository>();

/*builder.Services.AddSingleton<TpayClient>();

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
});*/

builder.Services.AddHttpClient<ITpayClient, TpayClient>((sp, http) =>
{
    var settings = sp.GetRequiredService<IOptions<TpaySettings>>().Value;

    if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
        throw new InvalidOperationException("Tpay:ApiBaseUrl is missing.");

    http.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
    http.DefaultRequestHeaders.Accept.Clear();
    http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

JsonConvert.DefaultSettings = () => new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver()
};

var host = builder.Build();

host.Run();
