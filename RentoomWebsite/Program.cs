using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RentoomBooking.SharedClasses.Database;
using RentoomWebsite.Services;


namespace RentoomWebsite
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");


            var apiBase = builder.Configuration["ApiBaseUrl"] ?? "/api/";

            builder.Services.AddHttpClient("FunctionsApi", c =>
            {
                c.BaseAddress = new Uri(apiBase);
            });

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            // builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            builder.Services.AddScoped<BookingDatabase>();
            builder.Services.AddScoped<IApartmentsService, ApartmentsService>();



            var host = builder.Build();

            // Example: log at startup
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Functions API base: {ApiBase}", apiBase);

            await host.RunAsync();
        }
    }
}
