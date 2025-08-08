using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.Api.Models;
using RentoomBooking.Api.Services;
using System.Net;
using System.Text.Json;

namespace RentoomBooking.Api;

public class ApartmentsApi
{
    private readonly IdoSellService _service;
    private readonly ILogger<ApartmentsApi> _logger;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ApartmentsApi(IdoSellService service, ILogger<ApartmentsApi> logger)
    {
        _service = service;
        _logger = logger;
    }

    // GET /api/apartments?city=Gdansk&top=50&continuationToken=...
    [Function("ListApartments")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apartments")] HttpRequestData req)
    {
        var q = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var city = q.Get("city");
        var token = q.Get("continuationToken");
        int.TryParse(q.Get("top"), out var top);
        top = top is > 0 and <= 200 ? top : 50;

        _logger.LogInformation("List apartments city={City} top={Top}", city, top);

        var result = await _service.QueryApartmentsAsync(token, top);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(result,"application/json; charset=utf-8");
        return resp;
    }
}
