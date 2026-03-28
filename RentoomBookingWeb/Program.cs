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
            builder.Services.AddApplicationInsightsTelemetry();
            
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddHttpClient();
            /* builder.Services.AddHttpClient("Api", client =>
             {
                 client.BaseAddress = new Uri("https://localhost:7241/");
             });*/

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
            builder.Services.AddScoped<ReservationWorkflowTelemetry>();
            builder.Services.AddScoped<GoogleAnalyticsService>();


            builder.Services.AddScoped<IPaymentFlowHandler, ReservationPaymentFlowHandler>();
            builder.Services.AddScoped<IPaymentFlowHandler, UpsellPaymentFlowHandler>();
            builder.Services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();
            //Customer Terms
            builder.Services.AddScoped<CustomerTermsRepository>();
            builder.Services.AddScoped<CustomerTermsService>();
            builder.Services.AddScoped<CookieConsentRepository>();
            builder.Services.AddScoped<CookieConsentService>();
            builder.Services.AddScoped<IUpsellCatalogService, UpsellCatalogService>();

            //upselle
            builder.Services.AddScoped<IUpsellOrderStore, UpsellOrderStore>();
            builder.Services.AddScoped<IUpsellOrderWorkflowService, UpsellOrderWorkflowService>();

            //upselle vouchery
            builder.Services.AddScoped<IUpsellVoucherProvisioningService, UpsellVoucherProvisioningService>();
            builder.Services.AddScoped<IUpsellVoucherCodeGenerator, UpsellVoucherCodeGenerator>();

            builder.Services.AddMemoryCache();
            //GUS
            builder.Services.Configure<GusApiSettings>(builder.Configuration.GetSection("GusApi"));
            builder.Services.AddScoped<IGusService, GusService>();

            builder.Services.AddScoped<IAvailabilityFinderService, AvailabilityFinderService>();
            builder.Services.AddScoped<IAvailabilityFinderService2, AvailabilityFinderService2>();

            //http context provider for absoulte urls - for tpay.
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ISiteBaseProvider, SiteBaseProvider>();


            //view scoped
            builder.Services.AddScoped<IApartmentsViewModel, ApartmentsViewModel>();
            
            builder.Services.AddDateRangePicker(config => { });

            //config
          

            // TPAY
            var TpaySection = builder.Configuration.GetSection("Tpay");

            var DummyIdoBookingApiKey = builder.Configuration.GetValue<string>("IdoBooking:UseDummy");
            tempLogger.LogInformation("Using Dummy IdoBooking Service (No Idobooking writes via API): {DummyIdoBookingApiKey}", DummyIdoBookingApiKey);

            builder.Services.Configure<TpaySettings>(TpaySection);

            builder.Services.AddHttpClient("Tpay", (sp, http) =>
            {
                var settings = sp.GetRequiredService<IOptions<TpaySettings>>().Value;

                if (string.IsNullOrWhiteSpace(settings.ApiBaseUrl))
                    throw new InvalidOperationException("Tpay:ApiBaseUrl is missing.");

                http.BaseAddress = new Uri(settings.ApiBaseUrl, UriKind.Absolute);
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            });

            builder.Services.AddScoped<ITpayClient>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Tpay");
                var opt = sp.GetRequiredService<IOptions<TpaySettings>>();
                var logger = sp.GetRequiredService<ILogger<TpayClient>>();
                return new TpayClient(http, opt, logger);
            });
            //TPAY END


            //storage options:
            builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

            var app = builder.Build();
            
            var supportedCultures = new[] { "en-US", "pl-PL" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            app.UseRequestLocalization(localizationOptions);

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            if (!app.Environment.IsProduction())
            {
                app.Use(async (context, next) =>
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive, nosnippet";
                        return Task.CompletedTask;
                    });

                    await next();
                });
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            
            // error 404
            app.UseStatusCodePagesWithReExecute("/404");

            app.UseAntiforgery();
            
            app.MapControllers();

            app.MapGet("/robots.txt", () =>
            {
                var content = app.Environment.IsProduction()
                    ? "User-agent: *\nAllow: /"
                    : "User-agent: *\nDisallow: /";

                return Results.Text(content, "text/plain");
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

         


            app.Run();
        }
    }
}
