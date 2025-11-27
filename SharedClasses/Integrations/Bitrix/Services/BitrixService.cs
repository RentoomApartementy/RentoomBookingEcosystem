using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public async Task<BitrixDealData> DownloadDealDetailsJsonAsync(int id, BitrixFieldsDefinition bitrixFieldsDefinition)
        {
            var endpointMethod = "crm.deal.get.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}?id={id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var deal = new BitrixDealData(content, bitrixFieldsDefinition);
            return deal;
        }

        public async Task<BitrixDealData> DownloadContactDetailsJsonAsync(int id, BitrixFieldsDefinition bitrixFieldsDefinition)
        {
            var endpointMethod = "crm.contact.get.json";
            var response = await client.GetAsync($"{baseURL}/{endpointMethod}?id={id}");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var deal = new BitrixDealData(content, bitrixFieldsDefinition);
            return deal;
        }





    }
}
