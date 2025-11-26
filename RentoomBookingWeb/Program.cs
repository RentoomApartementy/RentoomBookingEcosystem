using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBookingWeb.Components;
using System.Globalization;

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

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
