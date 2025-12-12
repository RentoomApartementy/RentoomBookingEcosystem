using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public class GetDealFunction
{
    private readonly ILogger<GetDealFunction> _logger;
    private readonly BitrixService _bitrixService;

    public GetDealFunction(ILogger<GetDealFunction> logger, BitrixService bitrixService)
    {
        _logger = logger;
        _bitrixService = bitrixService;
    }

    [Function("GetDealFunction")]
    public async Task<IActionResult> GetDeal([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "bitrix/deals/{id}")] HttpRequest req, int id)
    {

        try
        {

            var dealFieldsDef = await _bitrixService.DownloadDealFieldsDefinitionJsonAsync();
            var dealdata = await _bitrixService.DownloadDealDetailsJsonAsync(id, dealFieldsDef);

            var customerFieldsDef = await _bitrixService.DownloadCustomerFieldsDefinitionJsonAsync();

            var customerId = Convert.ToInt16(dealdata.DealData.FirstOrDefault(m => m.FieldID == "CONTACT_ID")?.Value);

            var customerdata = new BitrixResponseObject();
            if (customerId > 0) 

            customerdata = await _bitrixService.DownloadContactDetailsJsonAsync(customerId, customerFieldsDef);

            var deal = new BitrixDealForm
            {
                CustomerInfo = customerdata,
                DealForm = dealdata,
            };

            return new OkObjectResult(deal);
        }
        catch (Exception ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

    }

   
}