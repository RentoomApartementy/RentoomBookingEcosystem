using BlazorDateRangePicker;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Descriptions.Database;
using RentoomBooking.SharedClasses.Services.Descriptions;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Database;
using RentoomBooking.SharedClasses.Integrations.Tpay;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using RentoomBooking.SharedClasses.Models.Storage;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.Bonuses;
using RentoomBooking.SharedClasses.Services.Cookies;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Payments;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;
using RentoomBookingWeb.Components;
using RentoomBookingWeb.Components.Features.Apartments.ViewModels;
using RentoomBooking.SharedClasses.Services.Gus;
using RentoomBooking.SharedClasses.Models.Gus;
using RentoomBooking.SharedFrontend.Localization;
using RentoomBookingWeb.Services;
using RentoomBookingWeb.Services.Localization;
using RentoomBookingWeb.Configuration;
using System.Globalization;
using System.Linq;

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
            builder.Services.AddHttpClient(IdoBookingConnectService.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            });
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
                {
                    "image/svg+xml"
                });
            });
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

            builder.Services.AddDbContextFactory<RappDescriptionsDbContext>(options =>
                options.UseNpgsql(rentoomAppConnectionString));

            builder.Services.AddScoped<IApartmentAiDescriptionService, ApartmentAiDescriptionService>();

            var footerEnvironmentInfo = FooterEnvironmentInfo.Create(
               builder.Environment,
               ("BookingDb", postgresConnectionString),
               ("RentoomAppDb", rentoomAppConnectionString ?? string.Empty));
            builder.Services.AddSingleton(footerEnvironmentInfo);

            builder.Services.AddScoped<ApartmentRepository>();
            builder.Services.AddScoped<FiltersRepository>();
            builder.Services.AddScoped<RappQrMaintService>();
            builder.Services.AddScoped<IIdoApartmentService, IdoApartmentService>();
            builder.Services.AddScoped<IApartmentMediaCatalogService, ApartmentMediaCatalogService>();
            builder.Services.AddScoped<IApartmentPhotoBlobStorage, ApartmentPhotoBlobStorage>();
            builder.Services.AddScoped<IApartmentMediaVariantGenerator, ApartmentMediaVariantGenerator>();
            builder.Services.AddScoped<IApartmentsService, ApartmentsService>();
            builder.Services.AddScoped<IdoSellService>();
            builder.Services.AddScoped<IIdoBookingConnectService, IdoBookingConnectService>();
            builder.Services.AddScoped<IIdoOfferService, IdoOfferService>();
            builder.Services.AddScoped<IRentoomOfferService, RentoomOfferService>();
            builder.Services.AddScoped<IApartmentSearchFiltersService, ApartmentSearchFiltersService>();
            
            builder.Services.AddScoped<ReservationWorkflowService>();
            builder.Services.AddScoped<IReservationWorkflowService>(sp => sp.GetRequiredService<ReservationWorkflowService>());
            builder.Services.AddScoped<IReservationWorkflowSyncOperations>(sp => sp.GetRequiredService<ReservationWorkflowService>());
            builder.Services.AddScoped<IReservationSyncService, ReservationSyncService>();
            builder.Services.AddScoped<IReservationStore, ReservationStore>();
            builder.Services.AddScoped<IMockTpayGateway, MockTpayGateway>();
            builder.Services.AddScoped<ITpayGateway, TpayOpenApiGateway>();
            builder.Services.AddScoped<BitrixService>();
            builder.Services.AddScoped<BitrixLeadCaptureService>();
            builder.Services.AddScoped<IGusService, GusService>();
            builder.Services.AddScoped<IRouteLocalizationService, RouteLocalizationService>();
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
            builder.Services.AddScoped<IBonusesService, BonusesService>();
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
            builder.Services.Configure<AnalyticsOptions>(builder.Configuration.GetSection("Analytics"));
          

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
            builder.Services.Configure<StorageOptions>(ApartmentPhotoBlobStorage.StorageOptionsName, builder.Configuration.GetSection(ApartmentPhotoBlobStorage.StorageOptionsName));
            builder.Services.Configure<ApartmentMediaVariantsOptions>(builder.Configuration.GetSection(ApartmentMediaVariantsOptions.SectionName));

            var app = builder.Build();
            
            var supportedCultures = SupportedLanguagesProvider.SupportedCultureNames.ToArray();
            const string defaultCulture = "pl-PL";
            var supportedCultureSet = new HashSet<string>(supportedCultures, StringComparer.OrdinalIgnoreCase);
            var supportedCultureInfos = supportedCultures
                .Select(CultureInfo.GetCultureInfo)
                .ToList();

            var localizationOptions = new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(defaultCulture),
                SupportedCultures = supportedCultureInfos,
                SupportedUICultures = supportedCultureInfos,
                RequestCultureProviders = new List<IRequestCultureProvider>
                {
                    new CustomRequestCultureProvider(context =>
                    {
                        var path = context.Request.Path.Value;
                        if (string.IsNullOrEmpty(path) || path == "/") return Task.FromResult<ProviderCultureResult?>(null);

                        var parts = path.TrimStart('/').Split('/');
                        var potentialCulture = parts[0];
                        
                        var matchedCulture = supportedCultures.FirstOrDefault(c => 
                            string.Equals(c, potentialCulture, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(c.Split('-')[0], potentialCulture, StringComparison.OrdinalIgnoreCase));

                        return Task.FromResult<ProviderCultureResult?>(matchedCulture != null 
                            ? new ProviderCultureResult(matchedCulture) 
                            : null);
                    }),
                    new CookieRequestCultureProvider
                    {
                        CookieName = CookieRequestCultureProvider.DefaultCookieName
                    },
                    new CustomRequestCultureProvider(context =>
                    {
                        var ua = context.Request.Headers[HeaderNames.UserAgent].ToString();
                        var isBot = !string.IsNullOrWhiteSpace(ua) &&
                                    (ua.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
                                     ua.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
                                     ua.Contains("google", StringComparison.OrdinalIgnoreCase));

                        return Task.FromResult<ProviderCultureResult?>(isBot
                            ? new ProviderCultureResult(defaultCulture, defaultCulture)
                            : null);
                    }),
                    new AcceptLanguageHeaderRequestCultureProvider
                    {
                        MaximumAcceptLanguageHeaderValuesToTry = 3
                    }
                }
            };

            app.UseHttpsRedirection();
            app.UseResponseCompression();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.Context.Request.Path.Value ?? string.Empty;
                    var headers = ctx.Context.Response.Headers;

                    if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains(".bundle.scp.", StringComparison.OrdinalIgnoreCase))
                    {
                        headers[HeaderNames.CacheControl] = "public,max-age=31536000,immutable";
                    }
                    else if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                             path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                             path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        headers[HeaderNames.CacheControl] = "public,max-age=3600";
                    }
                }
            });

            app.UseRouting();

            app.UseMiddleware<LocalizedRoutingMiddleware>();
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

            // DIAGNOSTIC LOGGING
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("WebRootPath: {Path}", app.Environment.WebRootPath);
            logger.LogInformation("ContentRootPath: {Path}", app.Environment.ContentRootPath);

            // 404 TRAP PROTECTION: Don't show HTML 404 for files
            app.Use(async (context, next) =>
            {
                await next();
                if (context.Response.StatusCode == 404 && context.Request.Path.Value!.Contains('.'))
                {
                    // If it's a file and it's a 404, stop here. Don't let UseStatusCodePages re-execute to /404 HTML.
                    return;
                }
            });
            // error 404
            app.UseStatusCodePagesWithReExecute("/404");

            app.UseAntiforgery();
            
            app.MapControllers();

            app.MapGet("/robots.txt", (HttpContext context) =>
            {
                var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
                var content = app.Environment.IsProduction()
                    ? $"User-agent: *\nAllow: /\nSitemap: {baseUrl}/sitemap.xml"
                    : $"User-agent: *\nDisallow: /\nSitemap: {baseUrl}/sitemap.xml";

                return Results.Text(content, "text/plain");
            });

            app.MapGet("/culture/set", (HttpContext context, string? culture, string? returnUrl) =>
            {
                var resolvedCulture = defaultCulture;
                if (!string.IsNullOrWhiteSpace(culture) && supportedCultureSet.Contains(culture))
                {
                    resolvedCulture = supportedCultures.First(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));
                }

                var safeReturnUrl = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";

                var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolvedCulture));
                context.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    cookieValue,
                    new CookieOptions
                    {
                        Path = "/",
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        IsEssential = true,
                        SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                        Secure = context.Request.IsHttps
                    });

                return Results.LocalRedirect(safeReturnUrl);
            });

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

         


            app.Run();
        }

        private static bool IsSafeLocalReturnUrl(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return false;
            }

            if (!returnUrl.StartsWith('/'))
            {
                return false;
            }

            if (returnUrl.StartsWith("//", StringComparison.Ordinal) ||
                returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
