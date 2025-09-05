using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;


namespace RentoomBooking.SharedClasses.Services
{
    public  class IdoSellService
    {
        private string? baseAPIUrl;
        private string? systemUser;
        private string? systemPwd;

        private readonly HttpClient _httpClient;
        private readonly Container _objectsContainer;
        private readonly Container _hashesContainer;

        private const string HashDocumentId = "all-object-hashes"; // ID for the hash document
        private const string HashPartitionKey = "/id"; // Partition key for the hash container
        private BookingDatabase _bookingDatabase;
        private ILogger<IdoSellService> _logger;

        public IdoSellService(HttpClient httpClient,BookingDatabase bookingDatabase,  IConfiguration configuration)//, CosmosClient cosmosClient)
        {
            _httpClient = httpClient;

            _bookingDatabase = bookingDatabase;

            baseAPIUrl = configuration["IDOBOOKING_BASE_API_URL"];
            systemUser = configuration["IDOBOOKING_API_USER"];
            systemPwd = configuration["IDOBOOKING_API_PWD"];
            if (string.IsNullOrEmpty(baseAPIUrl))
            {
                _logger.LogError("IDOBOOKING_BASE_API_URL is not configured in local.settings.json or environment variables.");
               
            }

        }

        public async Task SyncAndStoreObjectsAsync()
        {

            var x= await _bookingDatabase.HasRecordsAsync();
            _logger.LogInformation($" found records: {x} ");
            _logger.LogInformation($"Syncing and storing apartment objects... ");
            var solutionname = SolutionNameClass.SolutionName_1;

            _logger.LogInformation($"Fetching for sultion {solutionname}");

            var existingHashes = await _bookingDatabase.GetExistingHashesAsync(_logger);
            var existingHashesDict = existingHashes.ToDictionary(h => h.objectId, h => h.hash);


            var allNewHashes = new List<ItemHash>();
            var objectsToCreate = new List<ApartmentObject>();
            var objectsToReplace = new List<ApartmentObject>();

            int currentPage = 1;
            int pageAll = 1;

            do
            {

                try
                {
                    var apiResponse = await FetchApartmentsByPageFromIdoSellAsync(currentPage);

                  
                    pageAll = apiResponse.Result?.Result?.PageAll ?? 0;
                    if (pageAll == 0)
                    {
                        _logger.LogWarning("apiResponse.Result or apiResponse.Result.Result is null, or PageAll is 0. Ending sync.");
                        break;
                    }

                    if (apiResponse?.Result?.Objects == null || apiResponse.Result.Objects.Count == 0)
                    {
                    _logger.LogInformation($"No apartments found on page {currentPage}. Ending sync.");
                    break;
                    }
               

                pageAll = apiResponse.Result.Result.PageAll;


                foreach (var obj in apiResponse.Result.Objects)
                {
                    obj.Id = obj.Id.ToString();
                    var obj_hash = ApartmentObjectHasher.ComputeHash(obj);
                 //   _logger.LogInformation($"apartments {obj.Name} hash {obj_hash}.");
                    allNewHashes.Add(new ItemHash { objectId = obj.Id, hash = obj_hash });

                    if (existingHashesDict.TryGetValue(obj.Id, out var existingHash))
                    {
                        if (existingHash != obj_hash)
                        {
                            objectsToReplace.Add(obj);
                        }
                    }
                    else
                    {
                        objectsToCreate.Add(obj);
                    }
                }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error fetching or processing page {currentPage}: {ex.Message}");
                    // Break on first error to prevent cascading failures
                    break;
                }
                currentPage++;

            } while (currentPage <= pageAll);


            if (objectsToCreate.Count != 0)
            {
                await _bookingDatabase.BulkCreateItemsAsync(objectsToCreate, _logger);
            }
            else
            {
                _logger.LogInformation("No new Apartments found to create.");
            }
            if (objectsToReplace.Count != 0)
            {
                await _bookingDatabase.BulkReplaceItemsAsync(objectsToReplace, _logger);
            }
            else
            {
                _logger.LogInformation("All Apartments are up to date.  Nothing to replace.");
            }

                // Update the hash document in a single operation
                await _bookingDatabase.UpdateHashesDocumentAsync(allNewHashes, _logger);


            _logger.LogInformation("Apartment objects synced and stored successfully.");
        }


        public async Task<List<ApartmentObject>> FetchApartmentsFromIdoSellAsync()
        {
           

            List<ApartmentObject> retList = new();

            var ret = await FetchApartmentsByPageFromIdoSellAsync(1);
            retList.AddRange(ret.Result.Objects);

            for (int pageNumber = 1; pageNumber <= ret.Result.Result.PageAll; pageNumber++)
            {
                ret = await FetchApartmentsByPageFromIdoSellAsync(pageNumber);
                retList.AddRange(ret.Result.Objects);
                _logger.LogInformation($"Number of apartments fetched so far: {retList.Count}");
            }

            return retList;

        }

        public async Task<ApartmentResponseType> FetchApartmentsByPageFromIdoSellAsync(int page)
        {
            
            string address = baseAPIUrl + "objects/getAll/34/json";
            _logger.LogInformation($"FetchApartmentsFromIdoSellAsync page={page}");
            var request = new
            {
                authenticate = new  { systemKey = GenerateKey(HashPassword(systemPwd)), systemLogin = systemUser, lang = "eng" },
                result = new  { page = page, number = 100 },
            
            };
            _logger.LogInformation($"pwd: {request.authenticate.systemKey}");

            HttpClient? client = new HttpClient();
            
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var requestString = new StringContent(System.Text.Json.JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(address, requestString);
            
            var responseContent = await response.Content.ReadAsStringAsync();


            ApartmentResponseType rsr = JsonConvert.DeserializeObject<ApartmentResponseType>(responseContent);
            client.Dispose();
            return rsr;



        }

        public static string GenerateKey(string hashedPassword)
        {
            System.Security.Cryptography.HashAlgorithm hash = System.Security.Cryptography.SHA1.Create();
            string date = System.String.Format("{0:yyyyMMdd}", System.DateTime.Now);
            string strToHash = date + hashedPassword;
            byte[] keyBytes, hashBytes;
            keyBytes = System.Text.Encoding.UTF8.GetBytes(strToHash);
            hashBytes = hash.ComputeHash(keyBytes);
            string hashedString = string.Empty;
            foreach (byte b in hashBytes)
            {
                hashedString += String.Format("{0:x2}", b);
            }

            return hashedString;
        }

        public static string HashPassword(string password)
        {
            System.Security.Cryptography.HashAlgorithm hash = System.Security.Cryptography.SHA1.Create();
            byte[] keyBytes, hashBytes;
            keyBytes = System.Text.Encoding.UTF8.GetBytes(password);
            hashBytes = hash.ComputeHash(keyBytes);
            string hashedString = string.Empty;
            foreach (byte b in hashBytes)
            {
                hashedString += String.Format("{0:x2}", b);
            }
            Console.WriteLine("hashed password: " + hashedString);
            return hashedString;
        }


        public Task<PagedResult<ApartmentObject>> QueryApartmentsAsync(
        string? continuationToken = null,
        int pageSize = 50)
        => _bookingDatabase.QueryApartmentsAsync(continuationToken, pageSize);


    }
}
