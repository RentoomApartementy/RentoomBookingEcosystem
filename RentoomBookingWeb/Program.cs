using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBookingWeb.Components;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;
using RentoomBookingWeb.Components.Features.ReservationWorkflow.Services;
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

            var postgresConnectionString = PostgresConnectionStringProvider.GetPostgresConnectionStringAsync(builder.Configuration, builder.Environment.IsDevelopment(), tempLogger).Result;
            if (string.IsNullOrWhiteSpace(postgresConnectionString))
            {
                throw new InvalidOperationException("RentoomDb connection string is missing.");
            }

            builder.Services.AddDbContextFactory<PostgresBookingDbContext>(options =>
                options.UseNpgsql(postgresConnectionString));

            builder.Services.AddScoped<PostgresBookingDatabase>();
          
            builder.Services.AddScoped<ApartmentRepository>();
            builder.Services.AddScoped<FiltersRepository>();
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
            builder.Services.AddScoped<IGusService, GusService>();


            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<IAvailabilityFinderService, AvailabilityFinderService>();

            //http context provider for absoulte urls - for tpay.
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ISiteBaseProvider, SiteBaseProvider>();


            //view scoped
            builder.Services.AddScoped<IApartmentsViewModel, ApartmentsViewModel>();

            //TPAY
            bool UseDevelopmentSettingsOnProd = true;
            var TpaySection = UseDevelopmentSettingsOnProd ? builder.Configuration.GetSection("TpayDev") : builder.Configuration.GetSection("TpayStage");
            
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

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAntiforgery();
            
            app.MapControllers();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

         


            app.Run();
        }
    }
}
