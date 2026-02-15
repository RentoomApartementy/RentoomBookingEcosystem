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
            _logger.LogInformation("GetTermsByResToken started for token: {resToken}", resToken);
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
                return res;
            }

            res.StatusCode = HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(terms));
            return res;
        }

        [Function("SaveTerms")]
        public async Task<HttpResponseData> SaveTerms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/terms/SaveTerms")]
            HttpRequestData req)
        {
            _logger.LogInformation("SaveTerms started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            try
            {
                var body = await req.ReadAsStringAsync();
                if (string.IsNullOrEmpty(body))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    await res.WriteStringAsync("Empty body.");
                    return res;
                }

                var entity = JsonConvert.DeserializeObject<TermsEntity>(body);

                if (entity == null || string.IsNullOrWhiteSpace(entity.ResToken))
                {
                    res.StatusCode = HttpStatusCode.BadRequest;
                    await res.WriteStringAsync("Invalid payload: ResToken is required.");
                    return res;
                }

                await _termsRepository.SaveTermsAsync(entity);

                res.StatusCode = HttpStatusCode.OK;
                res.Headers.Add("Content-Type", "application/json; charset=utf-8");
                await res.WriteStringAsync(JsonConvert.SerializeObject(entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving terms.");
                res.StatusCode = HttpStatusCode.InternalServerError;
                await res.WriteStringAsync("An internal error occurred.");
            }

            return res;
        }
    }
}