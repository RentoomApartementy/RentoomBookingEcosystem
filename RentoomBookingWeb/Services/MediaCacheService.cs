using System.Collections.Concurrent;
using System.Globalization;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;

namespace RentoomBookingWeb.Services
{
    public class MediaCacheService
    {
        // ObjectMedium carries the alt text in the visitor's language, so the culture is part of the key.
        private readonly ConcurrentDictionary<(int ApartmentId, string Culture), List<ObjectMedium>> _cache = new();

        private static (int, string) BuildKey(int apartmentId, string? culture)
            => (apartmentId, culture ?? CultureInfo.CurrentUICulture.Name);

        public async Task<List<ObjectMedium>> GetOrFetchMediaAsync(int apartmentId, Func<Task<List<ObjectMedium>>> fetchFactory, string? culture = null)
        {
            var key = BuildKey(apartmentId, culture);
            if (_cache.TryGetValue(key, out var cachedMedia) && cachedMedia != null && cachedMedia.Any())
            {
                return cachedMedia;
            }

            var fetchedMedia = await fetchFactory();
            if (fetchedMedia != null && fetchedMedia.Any())
            {
                _cache.AddOrUpdate(key, fetchedMedia, (_, _) => fetchedMedia);
            }
            return fetchedMedia ?? new List<ObjectMedium>();
        }

        public bool TryGetCachedMedia(int apartmentId, out List<ObjectMedium>? media, string? culture = null)
        {
            return _cache.TryGetValue(BuildKey(apartmentId, culture), out media);
        }

        public void PrimeMedia(int apartmentId, IReadOnlyCollection<ObjectMedium>? media, string? culture = null)
        {
            if (apartmentId <= 0 || media == null || media.Count == 0)
            {
                return;
            }

            var key = BuildKey(apartmentId, culture);
            _cache.AddOrUpdate(key, media.ToList(), (_, _) => media.ToList());
        }

        public void PrimeMediaBatch(IReadOnlyDictionary<int, List<ObjectMedium>> mediaByApartmentId, string? culture = null)
        {
            if (mediaByApartmentId == null || mediaByApartmentId.Count == 0)
            {
                return;
            }

            foreach (var entry in mediaByApartmentId)
            {
                PrimeMedia(entry.Key, entry.Value, culture);
            }
        }
    }
}
