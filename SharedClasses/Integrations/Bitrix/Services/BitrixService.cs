using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.Client;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Configuration;
using Microsoft.Extensions.Configuration;
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
    public sealed record BitrixDealLookupResult(int DealId, int? ContactId, int MatchCount);
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
        public const string IdoReservationIdFieldName = "UF_CRM_1768835556855";
        private string? _portalTimeZoneId;
        private TimeSpan? _serverUtcOffset;
        //private readonly  _rentoomDbContext;
        //   private SessionStorageService _sessionStorage;
        private readonly HttpClient client;
        private readonly string baseURL;

        public BitrixService(
            //RentoomDbContext rentoomDbContext,
            ///SessionStorageService sessionStorage, 
            HttpClient httpClient,
            IConfiguration configuration)
        {
          //  _rentoomDbContext = rentoomDbContext;
          //  _sessionStorage = sessionStorage;
            client = httpClient;
            var bitrixDomain = BitrixConfiguration.GetDomain(configuration).TrimEnd('/');
            var userIdForWebhook = BitrixConfiguration.GetUserIdForWebhook(configuration);
            var webhookId = BitrixConfiguration.GetWebhookId(configuration);
            baseURL = $"{bitrixDomain}/{userIdForWebhook}/{webhookId}";
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

        public async Task<string> GetPortalTimeZoneIdAsync()
        {
            if (!string.IsNullOrWhiteSpace(_portalTimeZoneId))
            {
                return _portalTimeZoneId;
            }

            var response = await client.GetAsync($"{baseURL}/user.current.json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.TryGetProperty("TIME_ZONE", out var timeZoneElement)
                && timeZoneElement.ValueKind == JsonValueKind.String)
            {
                var timeZoneId = timeZoneElement.GetString();
                if (!string.IsNullOrWhiteSpace(timeZoneId))
                {
                    _portalTimeZoneId = timeZoneId;
                    return timeZoneId;
                }
            }

            throw BitrixError(content, doc);
        }

        public async Task<string> GetServerTimeAsync()
        {
            var response = await client.GetAsync($"{baseURL}/server.time.json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.String)
            {
                return resultElement.GetString()!;
            }

            throw BitrixError(content, doc);
        }

        public async Task<TimeSpan> GetServerUtcOffsetAsync()
        {
            if (_serverUtcOffset.HasValue)
            {
                return _serverUtcOffset.Value;
            }

            var response = await client.GetAsync($"{baseURL}/server.time.json");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("result", out var resultElement)
                && resultElement.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(resultElement.GetString(), out var parsed))
            {
                _serverUtcOffset = parsed.Offset;
                return parsed.Offset;
            }
            throw BitrixError(content, doc);
        }


        public async Task<Dictionary<string, string?>> GetDealRawFieldsAsync(int dealId, params string[] fieldNames)
        {
            using var doc = await PostAsync("crm.deal.get.json", new { id = dealId });

            if (!doc.RootElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Object)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }

            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldName in fieldNames)
            {
                result[fieldName] = resultElement.TryGetProperty(fieldName, out var valueElement)
                    ? valueElement.ToString()
                    : null;
            }

            return result;
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

        public async Task<int> AddContactAsync(CreateContactRequest NewContactData, string? contactTypeId = "UC_YIVWK8") //contactTypeId = "UC_YIVWK8" = Gość Rentoom
        {
            var endpointMethod = "crm.contact.add.json";
            
            var fields = new Dictionary<string, object?>
            {
                ["NAME"] = NewContactData.FirstName,
                ["LAST_NAME"] = NewContactData.LastName,
                ["OPENED"] = "Y",
                ["TYPE_ID"] = contactTypeId,
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

        public async Task UpdateContactAdditionalTermsAsync(int contactId, List<CustomerAgreedTermDto> terms)
        {
            var endpointMethod = "crm.contact.update.json";
            var fields = new Dictionary<string, object?>();
            
                //RB_Zgoda_MKT_Komunikacja_Email
                fields["UF_CRM_1773765574161"] = terms.FirstOrDefault(t => t.TermsSourceType == "marketing_email")?.IsAccepted == true ? "Y" : "N";
                
                //RB_Zgoda_MKT_Komunikacja_Whatsapp
                fields["UF_CRM_1773765547122"] = terms.FirstOrDefault(t => t.TermsSourceType == "marketing_whatsapp")?.IsAccepted == true ? "Y" : "N";
                
                //RB_Zgoda_MKT_Komunikacja_Whatsapp
                fields["UF_CRM_1773765547122"] = terms.FirstOrDefault(t => t.TermsSourceType == "marketing_email")?.IsAccepted == true ? "Y" : "N";

            //RB_Zgoda_MKT_Komunikacja_Telefon
            fields["UF_CRM_1773765599754"] = terms.FirstOrDefault(t => t.TermsSourceType == "marketing_email")?.IsAccepted == true ? "Y" : "N";
            
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

        public async Task<int> UpsertContactByEmailAsync(CreateContactRequest contact, string? contactTypeId = "UC_YIVWK8")
        {
            var existingId = await FindContactIdByEmailAsync(contact.Email);
            if (existingId.HasValue)
            {
                await UpdateContactAsync(existingId.Value, contact);
                return existingId.Value;
            }

            return await AddContactAsync(contact, contactTypeId);
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
            => await AddDealAsync(BuildDealFields(req));

        public async Task<int> UpsertDealAsync(int? dealId, CreateDealRequest createRequest, IDictionary<string, object?> updateFields)
        {
            ArgumentNullException.ThrowIfNull(createRequest);
            ArgumentNullException.ThrowIfNull(updateFields);

            var existingDealId = await ResolveExistingDealIdAsync(dealId, createRequest.CustomFields, updateFields);
            if (existingDealId.HasValue)
            {
                await UpdateDealAsync(existingDealId.Value, updateFields);
                return existingDealId.Value;
            }

            var createFields = MergeFields(BuildDealFields(createRequest), updateFields);
            return await AddDealAsync(createFields);
        }

        private async Task<int> AddDealAsync(IDictionary<string, object?> fields)
        {
            var endpointMethod = "crm.deal.add.json";

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

        private async Task<int?> ResolveExistingDealIdAsync(
            int? dealId,
            IDictionary<string, object?>? createCustomFields,
            IDictionary<string, object?> updateFields)
        {
            if (dealId.HasValue)
            {
                var dealIdFromBitrix = await FindDealIdAsync(new Dictionary<string, object?>
                {
                    ["ID"] = dealId.Value
                });

                if (dealIdFromBitrix.HasValue)
                {
                    return dealIdFromBitrix.Value;
                }
            }

            var idoReservationId = GetIntFieldValue(updateFields, IdoReservationIdFieldName)
                ?? GetIntFieldValue(createCustomFields, IdoReservationIdFieldName);

            if (idoReservationId.HasValue)
            {
                return await FindDealIdAsync(new Dictionary<string, object?>
                {
                    [IdoReservationIdFieldName] = idoReservationId.Value
                });
            }

            return null;
        }

        private async Task<int?> FindDealIdAsync(IDictionary<string, object?> filter)
        {
            using var doc = await PostAsync("crm.deal.list.json", new
            {
                filter,
                select = new[] { "ID" },
                order = new { ID = "DESC" }
            });

            if (!doc.RootElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Array)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }

            foreach (var item in resultElement.EnumerateArray())
            {
                if (!item.TryGetProperty("ID", out var idElement))
                {
                    continue;
                }

                if (TryGetInt32(idElement, out var id))
                {
                    return id;
                }
            }

            return null;
        }

        public async Task<BitrixDealLookupResult?> FindLatestDealByIdoReservationIdAsync(int idoReservationId)
        {
            if (idoReservationId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(idoReservationId), "Ido reservation id must be positive.");
            }

            using var doc = await PostAsync("crm.deal.list.json", new
            {
                filter = new Dictionary<string, object?>
                {
                    [IdoReservationIdFieldName] = idoReservationId
                },
                select = new[] { "ID", "CONTACT_ID" },
                order = new { ID = "DESC" }
            });

            if (!doc.RootElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Array)
            {
                throw BitrixError(doc.RootElement.GetRawText(), doc);
            }

            var matchCount = 0;

            foreach (var item in resultElement.EnumerateArray())
            {
                if (!item.TryGetProperty("ID", out var idElement) || !TryGetInt32(idElement, out var dealId))
                {
                    continue;
                }

                matchCount++;
                int? contactId = null;
                if (item.TryGetProperty("CONTACT_ID", out var contactElement)
                    && TryGetInt32(contactElement, out var parsedContactId))
                {
                    contactId = parsedContactId;
                }

                return new BitrixDealLookupResult(dealId, contactId, Math.Max(matchCount, resultElement.GetArrayLength()));
            }

            return null;
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

        private static Dictionary<string, object?> BuildDealFields(CreateDealRequest req)
        {
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

            return fields;
        }

        private static Dictionary<string, object?> MergeFields(
            IDictionary<string, object?> source,
            IDictionary<string, object?> overrides)
        {
            var merged = new Dictionary<string, object?>(source);
            foreach (var field in overrides)
            {
                merged[field.Key] = field.Value;
            }

            return merged;
        }

        private static int? GetIntFieldValue(IDictionary<string, object?>? fields, string fieldName)
        {
            if (fields is null || !fields.TryGetValue(fieldName, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                int intValue => intValue,
                long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                JsonElement jsonElement when TryGetInt32(jsonElement, out var parsed) => parsed,
                _ => null
            };
        }

        private static bool TryGetInt32(JsonElement element, out int value)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
            {
                return true;
            }

            value = default;
            return false;
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
