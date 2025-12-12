using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using System.Text.Json;

namespace RentoomBooking.Api.Integrations.BitrixFunctions;

public class CreateContactFunction
{
    private readonly ILogger<GetDealFunction> _logger;
    private readonly BitrixService _bitrixService;

    public CreateContactFunction(ILogger<GetDealFunction> logger, BitrixService bitrixService)
    {
        _logger = logger;
        _bitrixService = bitrixService;
    }

    [Function("CreateBitrixContact")]
    public async Task<IActionResult> CreateContact(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bitrix/contact")] HttpRequest req)
    {
        _logger.LogInformation("CreateBitrixContact start.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

        CreateContactRequest contactData;
        try
        {
            contactData = JsonSerializer.Deserialize<CreateContactRequest>(
                requestBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blad przy deserializacji jsona");
            return new BadRequestObjectResult("Invalid JSON payload.");
        }

        if (contactData == null ||
            string.IsNullOrWhiteSpace(contactData.FirstName) ||
            string.IsNullOrWhiteSpace(contactData.LastName) ||
            string.IsNullOrWhiteSpace(contactData.Email))
        {
            return new BadRequestObjectResult("FirstName, LastName and Email nieuzupelnio e.");
        }



        try
        {
            int contactId = await _bitrixService.AddContactAsync(contactData);

            return new OkObjectResult(new
            {
                ContactId = contactId,
                Message = "Utworzono kontakt w Bitrix24."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Blad podczas tworzenia kontktu");
            return new StatusCodeResult(StatusCodes.Status502BadGateway);
        }
    }

    [Function("GetBitrixContact")]
    public async Task<IActionResult> GetBitrixContact([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "bitrix/contact/{id}")] HttpRequest req, int id)
    {

        try
        {

            var customerFieldsDef = await _bitrixService.DownloadCustomerFieldsDefinitionJsonAsync();

            var customerdata = new BitrixResponseObject();
            if (id > 0)

                customerdata = await _bitrixService.DownloadContactDetailsJsonAsync(id, customerFieldsDef);

            return new OkObjectResult(customerdata);
        }
        catch (Exception ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

    }

}