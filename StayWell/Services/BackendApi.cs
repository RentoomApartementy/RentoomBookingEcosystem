using System.Net.Http.Json;
using System.Text.Json;

namespace RentoomBooking.StayWell.Services
{
    public class BackendApi
    {

        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _json;

        public BackendApi(IHttpClientFactory factory, JsonSerializerOptions json)
        {
            _http = factory.CreateClient("FunctionsApi");
            _json = json;
        }


      


    }
}
