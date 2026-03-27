using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public class GetDealFunction
{
    private readonly ILogger<GetDealFunction> _logger;
    private readonly BitrixService _bitrixService;
    private readonly IConfiguration _configuration;

    public GetDealFunction(ILogger<GetDealFunction> logger, BitrixService bitrixService, IConfiguration configuration)
    {
        _logger = logger;
        _bitrixService = bitrixService;
        _configuration = configuration;
    }

    [Function("GetDealFunction")]
    public async Task<IActionResult> BX_GetDeal([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "bitrix/deals/{id}")] HttpRequest req, int id)
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


    [Function("AddDeal")]
    public async Task<IActionResult> BX_AddDeal([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bitrix/deals/add")] HttpRequest req)
    {

        try
        {

            var pipelines = await _bitrixService.GetDealPipelinesAsync();
            var pipelineName = BitrixConfiguration.GetReservationPipelineName(_configuration);
            var rentalPipelineId = pipelines.Single(p => string.Equals(p.Name, pipelineName, StringComparison.OrdinalIgnoreCase)).Id;
            var stages = await _bitrixService.GetDealStagesAsync(rentalPipelineId);
            var newStageId = stages.Single(s => s.Name == "W toku").StageId;

            var assignedByUserId = BitrixConfiguration.GetAssignedByUserId(_configuration);
            var dealId =  await _bitrixService.AddDealAsync(new CreateDealRequest(
                                                                                    Title: "Booking #12345",
                                                                                    CategoryId: rentalPipelineId,
                                                                                    StageId: newStageId,
                                                                                    AssignedById: assignedByUserId,
                                                                                    Opportunity: 1500,
                                                                                    CurrencyId: "PLN",
                                                                                    ContactId: 628
                                                                                ));
            
            //var dealId = -1;  

            return new OkObjectResult(new {dealId, stages, pipelines });
        }
        catch (Exception ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

    }

    [Function("GetDealEmailActivities")]
    public async Task<IActionResult> BX_GetDealEmailActivities([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bitrix/deals/{id}/email-activities")] HttpRequest req, int id)
    {
        try
        {
            var activities = await _bitrixService.ListDealEmailActivitiesAsync(id);
            return new OkObjectResult(activities);
        }
        catch (Exception ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
    }

}
