using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.Client;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Bitrix.Services
{
    public sealed record BitrixDealPipeline(int Id, string Name);
    public sealed record BitrixDealStage(string StageId, string Name);
    public sealed record CreateDealRequest(
        string Title,
        int CategoryId,
        string StageId,
        int? AssignedById = null,
        decimal? Opportunity = null,
        string? CurrencyId = null,
        int? ContactId = null,
        int? CompanyId = null,
        IDictionary<string, object?>? CustomFields = null
    );


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


        public async Task<int> AddContactAsync(ClientAddRequestClient NewContactData, int resId,int assignedByBitrixUserId)
        {
            var endpointMethod = "crm.contact.add.json";

            var payload = new 
            {
                fields = new
                {
                    UF_CRM_1764281071779 = resId, //id rezerwacji IDB
                    NAME = NewContactData.FirstName,
                    LAST_NAME = NewContactData.LastName,
                    OPENED = "Y",
                    TYPE_ID = "UC_YIVWK8",
                    SOURCE_ID = "WEB",
                    ASSIGNED_BY_ID = assignedByBitrixUserId,
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

        public async Task<int> AddContactAsync(CreateContactRequest NewContactData)
        {
            var endpointMethod = "crm.contact.add.json";

            var fields = new Dictionary<string, object?>
            {
                ["NAME"] = NewContactData.FirstName,
                ["LAST_NAME"] = NewContactData.LastName,
                ["OPENED"] = "Y",
                ["TYPE_ID"] = "UC_YIVWK8",
                ["SOURCE_ID"] = "WEB",
                ["PHONE"] = new[]
                {
                    new { VALUE = NewContactData.Phone, VALUE_TYPE = "WORK" }
                },
                ["EMAIL"] = new[]
                {
                    new { VALUE = NewContactData.Email, VALUE_TYPE = "WORK" }
                }
            };

            if (NewContactData.AssignedById.HasValue)
            {
                fields["ASSIGNED_BY_ID"] = NewContactData.AssignedById.Value;
            }

            if (NewContactData.ReservationId.HasValue)
            {
                fields["UF_CRM_1764281071779"] = NewContactData.ReservationId.Value;
            }

            ApplyContactCustomFields(fields, NewContactData);

            var payload = new
            {
                fields,
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

        public async Task<int?> FindContactIdByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            using var doc = await PostAsync("crm.contact.list.json", new
            {
                filter = new { EMAIL = email },
                select = new[] { "ID" },
                order = new { ID = "ASC" }
            });

            if (!doc.RootElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var item in resultElement.EnumerateArray())
            {
                if (item.TryGetProperty("ID", out var idElement)
                    && int.TryParse(idElement.GetString(), out var id))
                {
                    return id;
                }
            }

            return null;
        }
        public async Task UpdateContactAsync(int contactId, CreateContactRequest updatedContact)
        {
            var endpointMethod = "crm.contact.update.json";

            var fields = new Dictionary<string, object?>
            {
                ["NAME"] = updatedContact.FirstName,
                ["LAST_NAME"] = updatedContact.LastName,
                ["OPENED"] = "Y",
                ["TYPE_ID"] = "UC_YIVWK8",
                ["SOURCE_ID"] = "WEB",
                ["PHONE"] = new[]
                {
                    new { VALUE = updatedContact.Phone, VALUE_TYPE = "WORK" }
                },
                ["EMAIL"] = new[]
                {
                    new { VALUE = updatedContact.Email, VALUE_TYPE = "WORK" }
                }
            };

            if (updatedContact.ReservationId.HasValue)
            {
                fields["UF_CRM_1764281071779"] = updatedContact.ReservationId.Value;
            }

            if (updatedContact.AssignedById.HasValue)
            {
                fields["ASSIGNED_BY_ID"] = updatedContact.AssignedById.Value;
            }

            ApplyContactCustomFields(fields, updatedContact);

            using var doc = await PostAsync(endpointMethod, new
            {
                id = contactId,
                fields,
                @params = new { REGISTER_SONET_EVENT = "Y" }
            });

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.True)
            {
                return;
            }

            throw BitrixError(doc.RootElement.GetRawText(), doc);
        }

        public async Task<int> UpsertContactByEmailAsync(CreateContactRequest contact)
        {
            var existingId = await FindContactIdByEmailAsync(contact.Email);
            if (existingId.HasValue)
            {
                await UpdateContactAsync(existingId.Value, contact);
                return existingId.Value;
            }

            return await AddContactAsync(contact);
        }

        
        public async Task<string> GetCurrentServerTime()
        {
            var endpointMethod = "server.time";

           
            using var doc = await PostAsync(endpointMethod,null);

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.String)
            {
                return resultElement.ToString();
            }

            throw BitrixError(doc.RootElement.GetRawText(), doc);
        }

        public async Task<int> AddDealAsync(CreateDealRequest req)
        {
            var endpointMethod = "crm.deal.add.json";

            var fields = new Dictionary<string, object?>
            {
                ["TITLE"] = req.Title,
                ["CATEGORY_ID"] = req.CategoryId,
                ["STAGE_ID"] = req.StageId,
                ["OPENED"] = "Y"
            };

            if (req.AssignedById.HasValue) fields["ASSIGNED_BY_ID"] = req.AssignedById.Value;
            if (req.Opportunity.HasValue) fields["OPPORTUNITY"] = req.Opportunity.Value;
            if (!string.IsNullOrWhiteSpace(req.CurrencyId)) fields["CURRENCY_ID"] = req.CurrencyId!;
            if (req.ContactId.HasValue) fields["CONTACT_ID"] = req.ContactId.Value;
            if (req.CompanyId.HasValue) fields["COMPANY_ID"] = req.CompanyId.Value;
            if (req.CustomFields is not null)
            {
                foreach (var customField in req.CustomFields)
                {
                    fields[customField.Key] = customField.Value;
                }
            }

            using var doc = await PostAsync(endpointMethod, new
            {
                fields,
                @params = new { REGISTER_SONET_EVENT = "Y" }
            });

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.Number)
            {
                return resultElement.GetInt32();
            }

            throw BitrixError(doc.RootElement.GetRawText(), doc);
        }

        public async Task UpdateDealAsync(int dealId, IDictionary<string, object?> fields)
        {
            var endpointMethod = "crm.deal.update.json";

            using var doc = await PostAsync(endpointMethod, new
            {
                id = dealId,
                fields,
                @params = new { REGISTER_SONET_EVENT = "Y" }
            });

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.True)
            {
                return;
            }

            throw BitrixError(doc.RootElement.GetRawText(), doc);
        }

        public async Task<List<BitrixDealPipeline>> GetDealPipelinesAsync()
        {
            var endpointMethod = "crm.category.list.json";

            // entityTypeId=2 => Deals
            using var doc = await PostAsync(endpointMethod, new { entityTypeId = 2 });

            if (!doc.RootElement.TryGetProperty("result", out var result)
                || !result.TryGetProperty("categories", out var categories)
                || categories.ValueKind != JsonValueKind.Array)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }

            var list = new List<BitrixDealPipeline>();
            foreach (var c in categories.EnumerateArray())
            {
                var id = c.GetProperty("id").GetInt32();
                var name = c.GetProperty("name").GetString() ?? $"Category {id}";
                list.Add(new BitrixDealPipeline(id, name));
            }

            return list;
        }

        public async Task<List<BitrixDealStage>> GetDealStagesAsync(int categoryId)
        {
            var endpointMethod = "crm.status.list.json";
            var entityId = categoryId == 0 ? "DEAL_STAGE" : $"DEAL_STAGE_{categoryId}";

            using var doc = await PostAsync(endpointMethod, new
            {
                filter = new { ENTITY_ID = entityId }
            });

            if (!doc.RootElement.TryGetProperty("result", out var result)
                || result.ValueKind != JsonValueKind.Array)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }

            var list = new List<BitrixDealStage>();
            foreach (var s in result.EnumerateArray())
            {
                var stageId = s.GetProperty("STATUS_ID").GetString() ?? "";
                var name = s.GetProperty("NAME").GetString() ?? stageId;
                list.Add(new BitrixDealStage(stageId, name));
            }

            return list;
        }


        public async Task<List<DealEmailActivityDto>> ListDealEmailActivitiesAsync(int dealId)
        {
            var endpointMethod = "crm.activity.list.json";

            using var doc = await PostAsync(endpointMethod, new
            {
                filter = new
                {
                    OWNER_TYPE_ID = 2, // Deals
                    OWNER_ID = dealId,
                    TYPE_ID = 4 // Email
                },
                select = new[]
                {
                    "ID",
                    "SUBJECT",
                    "PROVIDER_ID",
                    "PROVIDER_TYPE_ID",
                    "STATUS",
                    "COMPLETED",
                    "START_TIME",
                    "END_TIME",
                    "CREATED",
                    "LAST_UPDATED",
                    "DIRECTION"
                },
                order = new { CREATED = "DESC" }
            });

            if (!doc.RootElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Array)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }


            var activities = new List<DealEmailActivityDto>();

            foreach (var activity in resultElement.EnumerateArray())
            {
                activities.Add(MapEmailActivity(activity));
            }



            return activities;
        }




        private async Task<JsonDocument> PostAsync(string endpointMethod, object? payload)
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{baseURL}/{endpointMethod}";
            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(responseContent);
        }

        private static void ApplyContactCustomFields(IDictionary<string, object?> fields, CreateContactRequest request)
        {
            fields["UF_CRM_1710748755545"] = request.TaxNumber ?? string.Empty;
            fields["UF_CRM_1711632923055"] = request.CompanyAddress ?? string.Empty;
            fields["UF_CRM_1711632270409"] = request.CompanyName ?? string.Empty;
            fields["UF_CRM_1773080308994"] = request.CompanyEmail ?? string.Empty;
        }

        private static Exception BitrixError(string responseContent, JsonDocument doc)
        {
            if (doc.RootElement.TryGetProperty("error_description", out var errDesc))
                return new Exception($"Bitrix24 error: {errDesc.GetString()}");

            if (doc.RootElement.TryGetProperty("error", out var err))
                return new Exception($"Bitrix24 error: {err.GetString()}");

            return new Exception($"Unexpected Bitrix24 response: {responseContent}");
        }


        private static DealEmailActivityDto MapEmailActivity(System.Text.Json.JsonElement activity)
        {
            return new DealEmailActivityDto
            {
                Id = GetString(activity, "ID"),
                Subject = GetString(activity, "SUBJECT"),
                ProviderId = GetString(activity, "PROVIDER_ID"),
                ProviderTypeId = GetString(activity, "PROVIDER_TYPE_ID"),
                Status = GetString(activity, "STATUS"),
                Completed = GetString(activity, "COMPLETED"),
                StartTime = GetDateTimeOffset(activity, "START_TIME"),
                EndTime = GetDateTimeOffset(activity, "END_TIME"),
                Created = GetDateTimeOffset(activity, "CREATED"),
                LastUpdated = GetDateTimeOffset(activity, "LAST_UPDATED"),
                Direction = GetString(activity, "DIRECTION")
            };
        }

        private static string? GetString(System.Text.Json.JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static DateTimeOffset? GetDateTimeOffset(System.Text.Json.JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }



    }
}
