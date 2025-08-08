using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RentoomBooking.Api;

public class RentoomFirstTestAzureFunction
{
    private readonly ILogger<RentoomFirstTestAzureFunction> _logger;

    public RentoomFirstTestAzureFunction(ILogger<RentoomFirstTestAzureFunction> logger)
    {
        _logger = logger;
    }

    [Function("RentoomFirstTestAzureFunction")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}