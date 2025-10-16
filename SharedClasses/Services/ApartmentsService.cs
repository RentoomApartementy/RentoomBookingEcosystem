using Microsoft.Azure.Cosmos;
using Microsoft.VisualBasic;
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
       
        Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default);

        Task<long> GetApartmentsCountAsync();


        Task<ApartmentObject?> GetApartmentByIdAsync(int objectId);
        Task<List<ApartmentObject>> GetApartmentsByFilterAsync(ApartmentQueryFilter filter, CancellationToken ct = default);

   //     Task<ObjectMediaResponseType?> GetMedia(int objectId, CancellationToken ct = default);
   //     Task<List<ObjectDescription>?> GetDescriptions(int objectId, string? language = null, CancellationToken ct = default);
   //Task<PagedResult<ApartmentObject>> GetApartmentsByPageAsync(string? continuationToken = null, int pageSize = 50);

      
        Task<PagedResult<ApartmentObject>> GetAllApartmentsList();

    }

      



    public class ApartmentsService : IApartmentsService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        ApartmentRepository _apartmentsRepository;
        private readonly IIdoBookingConnectService _idoConnect;

       
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

        public async Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default)
        {
            var result = await _apartmentsRepository.QueryApartmentsAsync(continuationToken, top);

            return result;
        }


        public async Task<PagedResult<ApartmentObject>?> GetApartmentsPagedByFilterAsync(
           string? continuationToken = null,
           int top = 50,
           CancellationToken ct = default)
        {
            var result = await _apartmentsRepository.QueryApartmentsAsync(continuationToken, top);

            return result;
        }


        public async Task<long> GetApartmentsCountAsync()
        {
            return await _apartmentsRepository.GetApartmentCountAsync();
        }

//        public Task<PagedResult<ApartmentObject>?> GetApartmentsByPageAsync(string? continuationToken = null, int pageSize = 50) => _apartmentsRepository.QueryApartmentsAsync(continuationToken, pageSize);


      

        public async Task<ApartmentObject?> GetApartmentByIdAsync(int objectId)
        {
            return await _apartmentsRepository.FindApartmentInCosmosDb(objectId);
        }

        public async Task<List<ApartmentObject>> GetApartmentsByFilterAsync(ApartmentQueryFilter filter, CancellationToken ct = default)
        {
            //if (filter.EqobjectIds == null) throw new ArgumentNullException(nameof(objectIds));

            return await _apartmentsRepository.GetApartmentsByFilterAsync(filter, ct);
        }


        public async Task<PagedResult<ApartmentObject>> GetAllApartmentsList()
        {
            var allResults = new List<ApartmentObject>();
            string? continuationToken = null;
            long totalCount = await _apartmentsRepository.GetApartmentCountAsync();

            do
            {
                var page = await _apartmentsRepository.QueryApartmentsAsync(continuationToken, 100);
                allResults.AddRange(page.Items);
                continuationToken = page.ContinuationToken;
            } while (continuationToken != null);

            return new PagedResult<ApartmentObject>(
                allResults,
                null,            
                allResults.Count,   
                totalCount
            );
        }
      

    }

}


