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
        
        ApartmentRepository _apartmentsRepository;
        private readonly IIdoBookingConnectService _idoConnect;



        public ApartmentsService(IHttpClientFactory factory, IIdoBookingConnectService idoConnect, ApartmentRepository apartmentsRepository)
        {
            _factory = factory;
          
            _idoConnect = idoConnect;
            _apartmentsRepository = apartmentsRepository;
        }

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

//     
        public async Task<ApartmentObject?> GetApartmentByIdAsync(int objectId)
        {
            return _apartmentsRepository.FindApartmentInPostgres(objectId);
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


