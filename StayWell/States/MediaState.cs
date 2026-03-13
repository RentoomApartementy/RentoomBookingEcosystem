using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.StayWell.Services;

namespace RentoomBooking.StayWell.States
{
    public class MediaState(BackendApi backendApi, LocalStorageService localStorageService)
    {
        private const string MediaCacheKeyPrefix = "staywell:media:first:";
        private List<ObjectMedium>? _media;
        private readonly BackendApi _backendApi = backendApi;
        private readonly LocalStorageService _localStorageService = localStorageService;
        private int? _currentObjectId;

        private sealed class CachedMediaItem
        {
            public string? Url { get; set; }
            public string? Extension { get; set; }
        }

        public bool IsLoading { get; set; }

        public List<ObjectMedium>? CurrentMedia
        {
            get => _media;
            private set
            {
                _media = value;
                NotifyStateChanged();
            }
        }

        public async Task<List<ObjectMedium>?> GetMediaAsync(int objectId)
        {
            if (_currentObjectId == objectId) return CurrentMedia;

            SetLoading(true);
            try
            {
                var cachedMedia = await TryGetCachedMediaAsync(objectId);
                if (cachedMedia is not null)
                {
                    _currentObjectId = objectId;
                    CurrentMedia = cachedMedia;
                    return CurrentMedia;
                }

                var media = await _backendApi.GetApartmentMediaAsync(objectId);
                if (media == null)
                {
                    ClearMedia();
                    return null;
                }

                _currentObjectId = objectId;
                CurrentMedia = media;

                await CacheFirstMediaAsync(objectId, media);

                return CurrentMedia;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                ClearMedia();
                return null;
            }
            finally
            {
                SetLoading(false);
            }
        }

        public event Action? OnChange;

        public void SetMedia(List<ObjectMedium>? media)
        {
            CurrentMedia = media;
            IsLoading = false;
            NotifyStateChanged();
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
            NotifyStateChanged();
        }

        public void ClearMedia()
        {
            CurrentMedia = null;
            _currentObjectId = null;
            IsLoading = false;
        }

        private async Task<List<ObjectMedium>?> TryGetCachedMediaAsync(int objectId)
        {
            var (exists, cached) = await _localStorageService.TryGetItemAsync<CachedMediaItem>(GetCacheKey(objectId));
            if (!exists || string.IsNullOrWhiteSpace(cached?.Url))
            {
                return null;
            }

            return
            [
                new ObjectMedium
                {
                    ObjectId = objectId,
                    Position = 0,
                    Url = cached.Url,
                    Extension = NormalizeExtension(cached.Extension) ?? GetExtensionFromUrl(cached.Url)
                }
            ];
        }

        private async Task CacheFirstMediaAsync(int objectId, List<ObjectMedium> media)
        {
            var first = media.FirstOrDefault(m =>
                    !string.IsNullOrWhiteSpace(m.Url) &&
                    (string.Equals(m.Extension, "jpg", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(m.Extension, "png", StringComparison.OrdinalIgnoreCase)))
                ?? media.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.Url));

            if (first is null || string.IsNullOrWhiteSpace(first.Url))
            {
                return;
            }

            var cached = new CachedMediaItem
            {
                Url = first.Url,
                Extension = NormalizeExtension(first.Extension) ?? GetExtensionFromUrl(first.Url)
            };

            await _localStorageService.SetItemAsync(GetCacheKey(objectId), cached);
        }

        private static string GetCacheKey(int objectId) => $"{MediaCacheKeyPrefix}{objectId}";

        private static string? NormalizeExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return null;
            }

            return extension.Trim().TrimStart('.').ToLowerInvariant();
        }

        private static string? GetExtensionFromUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var withoutQuery = url.Split('?', '#')[0];
            var idx = withoutQuery.LastIndexOf('.');
            if (idx < 0 || idx == withoutQuery.Length - 1)
            {
                return null;
            }

            return withoutQuery[(idx + 1)..].ToLowerInvariant();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
