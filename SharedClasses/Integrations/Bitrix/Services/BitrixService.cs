using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Bitrix.Services
{
    public class BitrixService
    {
        //private readonly  _rentoomDbContext;
     //   private SessionStorageService _sessionStorage;
        private readonly HttpClient client;

        private string baseURL = "https://b24-grfccp.bitrix24.pl/rest/208/n5tri19od1ylw2fn";

        public BitrixService(
            //RentoomDbContext rentoomDbContext,
            ///SessionStorageService sessionStorage, 
            HttpClient httpClient)
        {
          //  _rentoomDbContext = rentoomDbContext;
          //  _sessionStorage = sessionStorage;
            client = httpClient;
        }

        public async Task<BitrixFieldsDefinition> DownloadDealFieldsDefinitionJsonAsync()
        {
            var endpointMethod = "crm.deal.fields.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var fields = new BitrixFieldsDefinition(content);
            return fields;
        }


        public async Task<BitrixFieldsDefinition> DownloadCustomerFieldsDefinitionJsonAsync()
        {
            var endpointMethod = "crm.contact.fields.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var fields = new BitrixFieldsDefinition(content);
            return fields;
        }

        public async Task<BitrixResponseObject> DownloadDealDetailsJsonAsync(int id, BitrixFieldsDefinition bitrixFieldsDefinition)
        {
            var endpointMethod = "crm.deal.get.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}?id={id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var deal = new BitrixResponseObject(content, bitrixFieldsDefinition);
            return deal;
        }

        public async Task<BitrixResponseObject> DownloadContactDetailsJsonAsync(int id, BitrixFieldsDefinition bitrixFieldsDefinition)
        {
            var endpointMethod = "crm.contact.get.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}?id={id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var deal = new BitrixResponseObject(content, bitrixFieldsDefinition);
            return deal;
        }


        public async Task<int> AddContactAsync(CreateContactRequest NewContactData)
        {
            var endpointMethod = "crm.contact.add.json";

            var payload = new 
            {
                fields = new
                {
                    UF_CRM_1764281071779 = NewContactData.ReservationId, //id rezerwacji IDB
                    NAME = NewContactData.FirstName,
                    LAST_NAME = NewContactData.LastName,
                    OPENED = "Y",
                    TYPE_ID = "UC_YIVWK8",
                    SOURCE_ID = "WEB",
                    ASSIGNED_BY_ID = NewContactData.AssignedById,
                    PHONE = new[]
                    {
                new { VALUE = NewContactData.Phone, VALUE_TYPE = "WORK" }
            },
                    EMAIL = new[]
                    {
                new { VALUE = NewContactData.Email, VALUE_TYPE = "WORK" }
            }
                },
                @params = new
                {
                    REGISTER_SONET_EVENT = "Y"
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{baseURL}/{endpointMethod}";
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseContent);

           
            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.Number)
            {
                return resultElement.GetInt32();
            }

            if (doc.RootElement.TryGetProperty("error_description", out var errorElement))
            {
                throw new Exception($"Bitrix24 error: {errorElement.GetString()}");
            }

            throw new Exception($"Unexpected Bitrix24 response: {responseContent}");
        }

        public async Task<BitrixResponseObject> GetBitrixContactByIdAsync(int id)
        {
            var customerFields = await DownloadCustomerFieldsDefinitionJsonAsync();

            var customerData = await DownloadContactDetailsJsonAsync(id, customerFields);
            return customerData;
        }

    }
}
