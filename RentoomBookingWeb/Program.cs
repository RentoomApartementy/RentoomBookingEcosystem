using Microsoft.AspNetCore.Localization;
using Microsoft.Azure.Cosmos;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Services;
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

           /* builder.Services.AddHttpClient("Api", client =>
            {
                client.BaseAddress = new Uri("https://localhost:7241/");
            });*/

            //var cosEndpoint = builder.Configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT");
            var cosEndpoint = CosmosEndpointProvider.GetCosmosEndpointAsync(builder.Configuration, builder.Environment.IsDevelopment()).Result;
            if (string.IsNullOrEmpty(cosEndpoint))
                throw new InvalidOperationException("AZURE_COSMOS_ENDPOINT configuration is missing.");

            var cosmosClient = new CosmosClient(cosEndpoint, new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

            builder.Services.AddSingleton(cosmosClient);

            builder.Services.AddScoped<BookingDatabase>();
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
