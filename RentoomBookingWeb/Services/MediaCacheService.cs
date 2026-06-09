using System.Collections.Concurrent;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;

namespace RentoomBookingWeb.Services
{
    public class MediaCacheService
    {
        private readonly ConcurrentDictionary<int, List<ObjectMedium>> _cache = new();

        public async Task<List<ObjectMedium>> GetOrFetchMediaAsync(int apartmentId, Func<Task<List<ObjectMedium>>> fetchFactory)
        {
            if (_cache.TryGetValue(apartmentId, out var cachedMedia) && cachedMedia != null && cachedMedia.Any())
            {
                return cachedMedia;
            }

            var fetchedMedia = await fetchFactory();
            if (fetchedMedia != null && fetchedMedia.Any())
            {
                _cache.AddOrUpdate(apartmentId, fetchedMedia, (key, old) => fetchedMedia);
            }
            return fetchedMedia ?? new List<ObjectMedium>();
        }

        public bool TryGetCachedMedia(int apartmentId, out List<ObjectMedium>? media)
        {
            return _cache.TryGetValue(apartmentId, out media);
        }

        public void PrimeMedia(int apartmentId, IReadOnlyCollection<ObjectMedium>? media)
        {
            if (apartmentId <= 0 || media == null || media.Count == 0)
            {
                return;
            }

            _cache.AddOrUpdate(apartmentId, media.ToList(), (_, _) => media.ToList());
        }

        public void PrimeMediaBatch(IReadOnlyDictionary<int, List<ObjectMedium>> mediaByApartmentId)
        {
            if (mediaByApartmentId == null || mediaByApartmentId.Count == 0)
            {
                return;
            }

            foreach (var entry in mediaByApartmentId)
            {
                PrimeMedia(entry.Key, entry.Value);
            }
        }
    }
}
