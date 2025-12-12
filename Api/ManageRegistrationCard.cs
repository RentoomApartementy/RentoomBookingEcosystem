using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.StayWell;
using System.Net;
using System.Threading.Tasks;

namespace RentoomBooking.Api
{
    public class ManageRegistrationCard
    {
        private readonly RegistrationCardRepository _registrationCardRepository;
        private readonly ILogger<ManageRegistrationCard> _logger;

        public ManageRegistrationCard(RegistrationCardRepository registrationCardRepository, ILogger<ManageRegistrationCard> logger)
        {
            _registrationCardRepository = registrationCardRepository;
            _logger = logger;
        }

        [Function("GetRegistrationCardByResToken")]
        public async Task<HttpResponseData> GetRegistrationCardByResToken(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "db/registrationcard/GetRegistrationCardByResToken/{resToken}")]
            HttpRequestData req,
            string resToken)
        {
            _logger.LogInformation("GetRegistrationCardByResToken started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();
            if (string.IsNullOrWhiteSpace(resToken))
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Provide resToken in path.");
                return res;
            }
            var registrationCard = await _registrationCardRepository.GetRegistrationCardByResTokenAsync(resToken);
            if (registrationCard == null)
            {
                res.StatusCode = HttpStatusCode.NotFound;
                await res.WriteStringAsync($"No registration card found for token {resToken}.");
                return res;
            }
            res.StatusCode = HttpStatusCode.OK;
            res.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await res.WriteStringAsync(JsonConvert.SerializeObject(registrationCard));
            return res;
        }

        [Function("SaveRegistrationCard")]
        public async Task<HttpResponseData> SaveRegistrationCard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/registrationcard/SaveRegistrationCard")]
    HttpRequestData req)
        {
            _logger.LogInformation("SaveRegistrationCard started at: {time}", DateTime.UtcNow);
            var res = req.CreateResponse();

            var body = await req.ReadAsStringAsync();
            var entity = JsonConvert.DeserializeObject<RegistrationCardEntity>(body);

            if (entity == null || string.IsNullOrWhiteSpace(entity.ResToken))
            {
                res.StatusCode = HttpStatusCode.BadRequest;
                await res.WriteStringAsync("Invalid payload.");
                return res;
            }
            await _registrationCardRepository.SaveRegistrationCardAsync(entity);

            res.StatusCode = HttpStatusCode.OK;
            await res.WriteStringAsync(JsonConvert.SerializeObject(entity));
            return res;
        }
    }
}
