using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RentoomBooking.SharedClasses;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RentoomBooking.StayWell
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            var apiBase = builder.Configuration["ApiBaseUrl"] ?? "/api/";

            builder.Services.AddHttpClient("FunctionsApi", c =>
            {
                c.BaseAddress = new Uri(apiBase);
            });

            
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
