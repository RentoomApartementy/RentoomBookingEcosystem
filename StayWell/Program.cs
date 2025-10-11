using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RentoomBooking.SharedClasses;
using RentoomBooking.StayWell.Services;
using RentoomBooking.StayWell.States;
using System.Text.Json;


namespace RentoomBooking.StayWell
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Resources";
            });

            builder.Services.AddScoped<ReservationState>();
            builder.Services.AddScoped<MediaState>();
            builder.Services.AddScoped<AmenitiesState>();

            var apiBase = builder.Configuration["ApiBaseUrl"] ?? "/api/";

            builder.Services.AddHttpClient("FunctionsApi", c =>
            {
                c.BaseAddress = new Uri("https://localhost:7238"+apiBase);
            });

            builder.Services.AddScoped<BackendApi>();

            builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            
            builder.Logging.SetMinimumLevel(LogLevel.Information);


            var host = builder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            var solutionname = SolutionNameClass.SolutionName_2;

            logger.LogInformation("Fetching for solution {SolutionName}", solutionname);

            await host.RunAsync();
        }
    }
}
