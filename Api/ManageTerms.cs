using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api
{
    public class ManageTerms
    {
        private readonly TermsRepository _termsRepository;
        private readonly ILogger<ManageTerms> _logger;

        public ManageTerms(TermsRepository termsRepository, ILogger<ManageTerms> logger)
        {
            _termsRepository = termsRepository;
            _logger = logger;
        }

        [Function("GetTermsByResToken")]
        public async Task<HttpResponseData> GetTermsByResToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/terms/GetTermsByResToken/{resToken}")]
            HttpRequestData req,
            string resToken)
        {
            _logger.LogInformation("GetTermsByResToken started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            if (string.IsNullOrWhiteSpace(resToken))
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide resToken in path.");
                return res;
            }

            var terms = await _termsRepository.GetTermsByResTokenAsync(resToken);
            if (terms == null)
            {
                res.StatusCode = HttpStatusCode.NotFound;
                await res.WriteStringAsync($"No terms found for token {resToken}.");
                return res;
            }

            res.StatusCode = HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(terms));
            return res;
        }

        [Function("AddTerms")]
        public async Task<HttpResponseData> AddTerms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/terms/AddTerms")]
            HttpRequestData req)
        {
            _logger.LogInformation("AddTerms started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            var body = await req.ReadAsStringAsync();
            var entity = JsonConvert.DeserializeObject<TermsEntity>(body);

            if (entity == null || string.IsNullOrWhiteSpace(entity.ResToken))
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Invalid payload.");
                return res;
            }

            await _termsRepository.AddTermsAsync(entity);

            res.StatusCode = HttpStatusCode.Created;
            await res.WriteStringAsync(JsonConvert.SerializeObject(entity));
            return res;
        }

        [Function("UpdateTerms")]
        public async Task<HttpResponseData> UpdateTerms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "db/terms/UpdateTerms/{resToken}")]
            HttpRequestData req,
            string resToken)
        {
            _logger.LogInformation("UpdateTerms started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            var body = await req.ReadAsStringAsync();
            var entity = JsonConvert.DeserializeObject<TermsEntity>(body);

            if (entity == null || string.IsNullOrWhiteSpace(resToken) || entity.ResToken != resToken)
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Invalid payload or token.");
                return res;
            }

            try
            {
                await _termsRepository.UpdateTermsAsync(entity);
                res.StatusCode = HttpStatusCode.OK;
                await res.WriteStringAsync(JsonConvert.SerializeObject(entity));
            }
            catch (InvalidOperationException ex)
            {
                res.StatusCode = HttpStatusCode.NotFound;
                await res.WriteStringAsync(ex.Message);
            }

            return res;
        }
    }
}