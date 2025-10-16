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
    public interface IAmenitiesService
    {
        /// <summary>
        /// Zwraca zestaw filtrów z Idobooking do wyświetlenia na stronie rentoom.
        /// Zestaw filtrów jest zawężony do tych zdefiniowany w CosmosDB
        /// Jesli chcemy zwiększyć ilość wyświetlanych filtrów należy dodać odpowiednie id takiego filtra z Idobooking do tablicy w CosmosDB.
        /// </summary>
        /// <param name="objectTypes"></param>
        /// <returns>List<ObjectTypesAmenities>?</returns>
        Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes);
    }

    public class AmenitiesService : IAmenitiesService
    {
        private readonly IHttpClientFactory _factory;
        BookingDatabase _bd;
        AmenitiesRepository _AmenitiesRepository;
        ApartmentRepository _ApartmentRepository;
        IdoSellService _IdoSellService;
        public AmenitiesService(IHttpClientFactory factory, BookingDatabase bd, AmenitiesRepository AmenitiesRepository, ApartmentRepository ApartmentRepository, IdoSellService idoSellService)
        {
            _factory = factory;
            _bd = bd;
            _AmenitiesRepository = AmenitiesRepository;
            _IdoSellService = idoSellService;
            _ApartmentRepository = ApartmentRepository;
        }


       public async Task<List<ObjectTypesAmenities>?> GetFilteredAmenitiesForObjectTypes(IEnumerable<IdoBookingObjectType> objectTypes)
        {
            var AllAmenities = await _IdoSellService.FetchAmenitiesForObjectTypesAsync(objectTypes);

            if (AllAmenities == null || AllAmenities.Count == 0)
                return AllAmenities;

            var filterValues = await GetAmenitiesFilterValuesAsync();

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
        }

        public async Task<int[]> GetAmenitiesFilterValuesAsync()
        {
            var filteres = await _AmenitiesRepository.GetAmenitiesFilterAsync();

            return filteres ?? Array.Empty<int>();
        }
    }
}
