using System.Collections.Concurrent;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;

namespace RentoomBookingWeb.Services
{
    public class MediaCacheService
    {
        private readonly ConcurrentDictionary<int, List<ObjectMedium>> _cache = new();

        // Metoda do pobierania (Lazy Load)
        public async Task<List<ObjectMedium>> GetOrFetchMediaAsync(int apartmentId, Func<Task<List<ObjectMedium>>> fetchFactory)
        {
            if (_cache.TryGetValue(apartmentId, out var cachedMedia))
            {
                return cachedMedia;
            }

            var fetchedMedia = await fetchFactory();
            if (fetchedMedia != null)
            {
                _cache.TryAdd(apartmentId, fetchedMedia);
            }
            return fetchedMedia ?? new List<ObjectMedium>();
        }

        // NOWA METODA: Sprawdź czy masz, ale nie pobieraj
        public bool TryGetCachedMedia(int apartmentId, out List<ObjectMedium>? media)
        {
            return _cache.TryGetValue(apartmentId, out media);
        }
    }
}