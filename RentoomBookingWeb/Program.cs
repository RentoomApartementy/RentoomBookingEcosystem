using Microsoft.Azure.Cosmos;
using RentoomBooking.SharedClasses.Database;
using RentoomBookingWeb.Components;
using RentoomBooking.SharedClasses.Services;

namespace RentoomBookingWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddHttpClient("Api", client =>
            {
                //var baseUrl = builder.Configuration["Api:BaseUrl"]!;
                var baseUrl = "https://localhost:7241/";
                client.BaseAddress = new Uri(baseUrl);
            });


            var cosendpoint = builder.Configuration.GetConnectionString("AZURE_COSMOS_ENDPOINT");
            //cosendpoint = builder.Configuration["AZURE_COSMOS_ENDPOINT"];
            if (string.IsNullOrEmpty(cosendpoint))
            {
                throw new InvalidOperationException("AZURE_COSMOS_ENDPOINT configuration is missing.");
            }


            var cosmosClient = new CosmosClient(cosendpoint, new CosmosClientOptions()
            {
                //ConnectionMode = ConnectionMode.Gateway,
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });

            builder.Services.AddSingleton(cosmosClient);

            //builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            // builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            builder.Services.AddScoped<BookingDatabase>();
            builder.Services.AddScoped<ApartmentRepository>();
            builder.Services.AddScoped<IApartmentsService, ApartmentsService>();


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
