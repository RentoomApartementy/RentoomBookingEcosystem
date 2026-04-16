using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RentoomBooking.SharedClasses;
using RentoomBooking.StayWell.Services;
using RentoomBooking.StayWell.States;
using System.Text.Json;
using ApartmentState = RentoomBooking.StayWell.States.ApartmentState;


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

            builder.Services.AddSingleton<LayoutState>();
            builder.Services.AddSingleton<ToastService>();
            builder.Services.AddSingleton<LocalStorageService>();
            builder.Services.AddSingleton<ReservationTokenService>();
            builder.Services.AddSingleton<GlobalizationService>();
            builder.Services.AddSingleton<FunctionsApiConcurrencyHandler>();

            builder.Services.AddScoped<ClipboardService>();
            builder.Services.AddScoped<ModalService>();
            builder.Services.AddScoped<BitrixService>();
            builder.Services.AddScoped<UpsellCartState>();

            builder.Services.AddScoped<ReservationState>();
            builder.Services.AddScoped<MediaState>();
            builder.Services.AddScoped<AmenitiesState>();
            builder.Services.AddScoped<ApartmentState>();
            builder.Services.AddScoped<LocksState>();
            builder.Services.AddScoped<TermsState>();
            builder.Services.AddScoped<RegistrationCardState>();
            builder.Services.AddScoped<CustomerAgreedTermsState>();

         


            //available upsells
            builder.Services.AddScoped<AvailableUpsellsState>();

            builder.Services.AddHttpClient("FunctionsApi", c =>
                {
                    if (builder.HostEnvironment.IsDevelopment())
                    {
                        // Local dev:
                        c.BaseAddress = new Uri("https://localhost:7238/api/");
                    }
                    else
                    {
                        // Azure Static Web App: functions api sa deployowane oddzielne, ale sa "podpiete" w Static Website (Settings->Api) na Azure - wiec powinny byc dostepny pod adresem /api
                        var appBase = new Uri(builder.HostEnvironment.BaseAddress);
                        c.BaseAddress = new Uri(appBase, "api/");
                    }
                })
                .AddHttpMessageHandler<FunctionsApiConcurrencyHandler>();

            builder.Services.AddScoped<BackendApi>();

            builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            
            builder.Logging.SetMinimumLevel(LogLevel.Information);


            var host = builder.Build();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            var solutionname = SolutionNameClass.SolutionName_2;

            logger.LogInformation("Fetching for solution {SolutionName}", solutionname);

            var config = builder.Configuration;
            var env = builder.Configuration["ASPNETCORE_ENVIRONMENT_STAYWELL"];
            logger.LogWarning("FOUND ENV: {env}", env);


            await host.RunAsync();
        }
    }
}
