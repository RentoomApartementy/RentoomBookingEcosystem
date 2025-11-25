using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.PostgresSeeder;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System.Net;
using System.Net.Http;
using System.Text;

namespace RentoomBooking.SharedClasses.Services
{
    public interface IApartmentSearchFiltersService
    {
        /// <summary>
        /// Zwraca zestaw filtrów z Idobooking do wyświetlenia na stronie rentoom.
        /// Zestaw filtrów jest zawężony do tych zdefiniowany w CosmosDB
        /// Jesli chcemy zwiększyć ilość wyświetlanych filtrów należy dodać odpowiednie id takiego filtra z Idobooking do tablicy w CosmosDB.
        /// </summary>
        /// <param name="objectTypes"></param>
        /// <returns>List<ObjectTypesAmenities>?</returns>
       // Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes);
        Task<List<SearchFilterDocument>> GetFiltersAsync();
        Task<bool> SaveFiltersAsync();
    }

    public class ApartmentSearchFiltersService : IApartmentSearchFiltersService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        FiltersRepository _FiltersRepository;
        ApartmentRepository _ApartmentRepository;
        IdoSellService _IdoSellService;
        private readonly PostgresBookingDatabase _postgresBookingDatabase;
        private readonly ILogger<ApartmentSearchFiltersService> _logger;
        public ApartmentSearchFiltersService(IHttpClientFactory factory, BookingDatabase bd, FiltersRepository FiltersRepository, ApartmentRepository ApartmentRepository, IdoSellService idoSellService, PostgresBookingDatabase postgresBookingDatabase, ILogger<ApartmentSearchFiltersService> logger)
        {
            _factory = factory;
            _bd = bd;
            _FiltersRepository = FiltersRepository;
            _IdoSellService = idoSellService;
            _ApartmentRepository = ApartmentRepository;
            _postgresBookingDatabase = postgresBookingDatabase ?? throw new ArgumentNullException(nameof(postgresBookingDatabase));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        /*  public async Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes)
           {
               var AllAmenities = await _IdoSellService.FetchAmenitiesForObjectTypesAsync(objectTypes);

               if (AllAmenities == null || AllAmenities.Count == 0)
                   return AllAmenities;

               var filterValues = await GetFiltersAsync();

               if (filterValues.Length == 0)
                   return AllAmenities;

               var filterSet = new HashSet<int>(filterValues);

               foreach (var objectTypeAmenities in AllAmenities)
               {
                   if (objectTypeAmenities?.ObjectAmenities == null)
                   {
                       continue;
                   }

                   objectTypeAmenities.ObjectAmenities = objectTypeAmenities.ObjectAmenities
                       .Where(a => filterSet.Contains(a.AmenityId))
                       .ToList();
               }

               return AllAmenities;
           }*/

        public async Task<List<SearchFilterDocument>> GetFiltersAsync()
        {
            var filteres = await _postgresBookingDatabase.GetAllSearchFiltersAsync(_logger);

            return filteres ?? [];
        }


        /// <summary>
        /// Wstępna populacja bazy - uruchom raz.
        /// </summary>
        /// 
        /// <returns>true</returns>
        public async Task<bool> SaveFiltersAsync()
        {
            var filters = SearchFiltersSeedData.BuildAmenitiesFilters();

            await _FiltersRepository.SaveFilters(filters, SearchFiltersSeedData.AmenitiesFilterGroupName, _logger);

            await _postgresBookingDatabase.SaveSearchFiltersAsync(filters, SearchFiltersSeedData.AmenitiesFilterGroupName, _logger);

            return true;
        }
    }
}
