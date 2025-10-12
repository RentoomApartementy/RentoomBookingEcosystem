using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IApartmentsService
    {
       
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncDirect(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default);

        Task<long> GetApartmentsCountAsync();

        Task<ObjectMediaResponseType?> GetMedia(int objectId, CancellationToken ct = default);
        Task<List<ObjectDescription>?> GetDescriptions(int objectId, string? language = null, CancellationToken ct = default);
        Task<List<ApartmentObject>> GetAllApartmentsList();
      
    }

      



    public class ApartmentsService : IApartmentsService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        ApartmentRepository _apartmentsRepository;
        private readonly IIdoBookingConnectService _idoConnect;

        private const string ObjectMediaGetEndpoint = "objects/getMedia/34/json";
        private const string HashDocumentId = "all-object-hashes"; // ID for the hash document


        public ApartmentsService(IHttpClientFactory factory, IIdoBookingConnectService idoConnect, BookingDatabase bd, ApartmentRepository apartmentsRepository)
        {
            _factory = factory;
            _bd = bd;
            _idoConnect = idoConnect;
            _apartmentsRepository = apartmentsRepository;
        }

     /*   public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncWithFunctionApi(
            string? continuationToken = null,
            int top = 50,
            CancellationToken ct = default)
        {
            var http = _factory.CreateClient("FunctionsApi");

            var sb = new StringBuilder("apartments?top=").Append(top);
            if (!string.IsNullOrWhiteSpace(continuationToken))
                sb.Append("&continuationToken=").Append(Uri.EscapeDataString(continuationToken));

            using var resp = await http.GetAsync(sb.ToString(), ct);
            resp.EnsureSuccessStatusCode();

            var jsonString = await resp.Content.ReadAsStringAsync(ct);

            
            return JsonConvert.DeserializeObject<PagedResult<ApartmentObject>>(jsonString);
        }
     */

        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsyncDirect(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default)
        {
            var result = await _bd.QueryApartmentsAsync(continuationToken, top);

            return result;
        }

        public async Task<long> GetApartmentsCountAsync()
        {
            return await _apartmentsRepository.GetApartmentCountAsync();
        }


        public async Task<ObjectMediaResponseType?> GetMedia(int objectId, CancellationToken ct = default)
        {
            var request = new ObjectMediaRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectId = objectId
            };
            return await _idoConnect.PostAsync<ObjectMediaRequestType, ObjectMediaResponseType>(ObjectMediaGetEndpoint, request, ct);
        }

        public async Task<List<ObjectDescription>?> GetDescriptions(int objectId, string? language = null, CancellationToken ct = default)
        {
            var request = new ObjectDescriptionsRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ParamsSearch = new ObjectDescriptionParamsSearch
                {
                    ObjectId = objectId,
                    Language = language,
                }
            };

            var ret = await _idoConnect.PostAsync<ObjectDescriptionsRequestType, ObjectDescriptionsResponseType>(ObjectMediaGetEndpoint, request, ct);
            return ret?.Result.ObjectDescriptions;
        }

        public async Task<List<ApartmentObject>> GetAllApartmentsList()
        {
            return await _apartmentsRepository.GetAllApartmentsList();
        }
      
    }
}
