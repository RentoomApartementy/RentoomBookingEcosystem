using BlazorDateRangePicker;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.Storage;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Cookies;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Payments;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;
using RentoomBookingWeb.Components;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;
using RentoomBooking.SharedClasses.Services.Gus;
using RentoomBooking.SharedClasses.Models.Gus;
using RentoomBookingWeb.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;

namespace RentoomBookingWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });
            
            builder.Services.AddControllers();
            
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddHttpClient();

            using var tempLoggerFactory = LoggerFactory.Create(lb =>
            {
                lb.AddConfiguration(builder.Configuration.GetSection("Logging"));
                lb.AddConsole();
                lb.AddDebug();
            });
            var tempLogger = tempLoggerFactory.CreateLogger("PostgresInit");

            //POSTGRESS:
            var postgresConnectionString = PostgresConnectionStringProvider.GetPostgresConnectionString(builder.Configuration, "POSTGRES_RENTOOM_BOOKING_DB_LOCAL", builder.Environment.IsDevelopment(), tempLogger);
            if (string.IsNullOrWhiteSpace(postgresConnectionString))
            {
                throw new InvalidOperationException("RentoomBookingDb connection string is missing.");
            }

            builder.Services.AddDbContextFactory<PostgresBookingDbContext>(options =>
                options.UseNpgsql(postgresConnectionString));

            builder.Services.AddScoped<PostgresBookingDatabase>();

            var rentoomAppConnectionString = PostgresConnectionStringProvider
               .GetPostgresConnectionString(builder.Configuration, "RentoomDbConnectionString", builder.Environment.IsDevelopment(), tempLogger);

            if (string.IsNullOrWhiteSpace(rentoomAppConnectionString))
            {
                throw new InvalidOperationException("RentoomAppDb connection string is missing.");
            }

            builder.Services.AddDbContext<QrMaintRappDbContext>(options =>
                options.UseNpgsql(rentoomAppConnectionString));

            builder.Services.AddDbContextFactory<RappPartnersDBContext>(options =>
                options.UseNpgsql(rentoomAppConnectionString));

            var footerEnvironmentInfo = FooterEnvironmentInfo.Create(
               builder.Environment,
               ("BookingDb", postgresConnectionString),
               ("RentoomAppDb", rentoomAppConnectionString ?? string.Empty));
            builder.Services.AddSingleton(footerEnvironmentInfo);

            builder.Services.AddScoped<ApartmentRepository>();
            builder.Services.AddScoped<FiltersRepository>();
            builder.Services.AddScoped<RappQrMaintService>();
            builder.Services.AddScoped<IIdoApartmentService, IdoApartmentService>();
            builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
            builder.Services.AddScoped<IdoSellService>();
            builder.Services.AddScoped<IIdoBookingConnectService, IdoBookingConnectService>();
            builder.Services.AddScoped<IIdoOfferService, IdoOfferService>();
            builder.Services.AddScoped<IRentoomOfferService, RentoomOfferService>();
            builder.Services.AddScoped<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
            
            builder.Services.AddScoped<IReservationWorkflowService, ReservationWorkflowService>();
            builder.Services.AddScoped<IReservationStore, ReservationStore>();
            builder.Services.AddScoped<IMockTpayGateway, MockTpayGateway>();
            builder.Services.AddScoped<ITpayGateway, TpayOpenApiGateway>();
            builder.Services.AddScoped<BitrixService>();
            builder.Services.AddScoped<BitrixLeadCaptureService>();
            builder.Services.AddScoped<IGusService, GusService>();
            builder.Services.AddScoped<MediaCacheService>();


            builder.Services.AddScoped<IPaymentFlowHandler, ReservationPaymentFlowHandler>();
            builder.Services.AddScoped<IPaymentFlowHandler, UpsellPaymentFlowHandler>();
            builder.Services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
            
            builder.Services.AddScoped<CustomerTermsRepository>();
            builder.Services.AddScoped<CustomerTermsService>();
            builder.Services.AddScoped<CookieConsentRepository>();
            builder.Services.AddScoped<CookieConsentService>();
            builder.Services.AddScoped<IUpsellCatalogService, UpsellCatalogService>();

            builder.Services.AddScoped<IUpsellOrderStore, UpsellOrderStore>();
            builder.Services.AddScoped<IUpsellOrderWorkflowService, UpsellOrderWorkflowService>();

            builder.Services.AddScoped<IUpsellVoucherProvisioningService, UpsellVoucherProvisioningService>();
            builder.Services.AddScoped<IUpsellVoucherCodeGenerator, UpsellVoucherCodeGenerator>();

            builder.Services.AddMemoryCache();
            builder.Services.Configure<GusApiSettings>(builder.Configuration.GetSection("GusApi"));

            builder.Services.AddScoped<IAvailabilityFinderService, AvailabilityFinderService>();
            builder.Services.AddScoped<IAvailabilityFinderService2, AvailabilityFinderService2>();

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ISiteBaseProvider, SiteBaseProvider>();

            builder.Services.AddScoped<IApartmentsViewModel, ApartmentsViewModel>();
            
            builder.Services.AddDateRangePicker(config => { });

            var TpaySection = builder.Configuration.GetSection("Tpay");
            builder.Services.Configure<TpaySettings>(TpaySection);

            builder.Services.AddHttpClient("Tpay", (sp, http) =>
            {
                var settings = sp.GetRequiredService<IOptions<TpaySettings>>().Value;
                if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
                    throw new InvalidOperationException("Tpay:ApiBaseUrl is missing.");

                http.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
            });

            builder.Services.AddScoped<ITpayClient>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Tpay");
                var opt = sp.GetRequiredService<IOptions<TpaySettings>>();
                var logger = sp.GetRequiredService<ILogger<TpayClient>>();
                return new TpayClient(http, opt, logger);
            });

            builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

            var app = builder.Build();

            // 1. Static files should be first to avoid localization/routing logic for assets
            app.UseStaticFiles();

            // 2. Routing must be BEFORE UseRequestLocalization for RouteDataRequestCultureProvider
            app.UseRouting();

            // 3. Configure Localization
            var supportedCultures = new[] { "pl-PL", "en-US" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            // Add short codes (pl, en) mapping or just add them to supported
            localizationOptions.AddSupportedCultures("pl", "en");
            localizationOptions.AddSupportedUICultures("pl", "en");

            localizationOptions.RequestCultureProviders.Insert(0, new RouteDataRequestCultureProvider());
            app.UseRequestLocalization(localizationOptions);

            // 4. Custom redirect logic for root
            app.Use(async (context, next) =>
            {
                var path = context.Request.Path.Value;
                if (path == "/" || path == "")
                {
                    var culture = context.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.TwoLetterISOLanguageName.ToLower() ?? "pl";
                    context.Response.Redirect($"/{culture}/");
                    return;
                }
                await next();
            });

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStatusCodePagesWithReExecute("/404");
            app.UseAntiforgery();
            
            app.MapControllers();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
