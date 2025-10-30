using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Enum;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
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
        public ApartmentSearchFiltersService(IHttpClientFactory factory, BookingDatabase bd, FiltersRepository FiltersRepository, ApartmentRepository ApartmentRepository, IdoSellService idoSellService)
        {
            _factory = factory;
            _bd = bd;
            _FiltersRepository = FiltersRepository;
            _IdoSellService = idoSellService;
            _ApartmentRepository = ApartmentRepository;
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
            var filteres = await _FiltersRepository.GetAllSearchFiltersAsync();

            return filteres ?? [];
        }


        /// <summary>
        /// Wstępna populacja bazy - uruchom raz.
        /// </summary>
        /// 
        /// <returns>true</returns>
        public async Task<bool> SaveFiltersAsync()
        {
           await _FiltersRepository.SeedAmenitiesFilters();

            return true;
        }
    }
}
