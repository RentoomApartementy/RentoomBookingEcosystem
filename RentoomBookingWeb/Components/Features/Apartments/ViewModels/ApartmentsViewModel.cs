using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using RentoomBooking.SharedClasses.Models.AvailableTerms;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBookingWeb.Helpers;
using RentoomBookingWeb.Services;
using RentoomBookingWeb.Services.Localization;

namespace RentoomBookingWeb.Components.Features.Apartments.ViewModels
{
    public class ApartmentsViewModel : IApartmentsViewModel
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IRentoomOfferService _rentoomOfferService;
        private readonly IIdoOfferService _idoOfferService;
        private readonly NavigationManager _navManager;
        private readonly IAvailabilityFinderService2 _availabilityFinder;
        private readonly IApartmentSearchFiltersService _filterService;
        private readonly ReservationWorkflowTelemetry _telemetry;
        private readonly GoogleAnalyticsService _googleAnalytics;
        private readonly MediaCacheService _mediaCache;
        private readonly IApartmentMediaCatalogService _apartmentMediaCatalogService;
        private readonly IRouteLocalizationService _routeService;
        private readonly ILogger<ApartmentsViewModel> _logger;
        private static readonly TimeSpan SuggestionsFetchTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MediaWarmTimeout = TimeSpan.FromSeconds(5);

        private string? _token;
        private const int PageSize = 12;
        private bool _isInitialized = false;
        private ApartmentFilters? _currentFilters = null;
        private CancellationTokenSource? _suggestionsCts;
        private readonly List<CancellationTokenSource> _incrementalSuggestionsCts = new();
        private readonly object _mediaWarmLock = new();
        private CancellationTokenSource _mediaWarmGenerationCts = new();

        private List<PricingOffer> _allMatchingOffers = new();
        private List<ApartmentObject> _matchingMetaItems = new();

        public List<ApartmentObject> Items { get; private set; } = new();
        public List<PricingOffer> Offers { get; private set; } = new();
        public Dictionary<int, PublicApartmentOffer> PublicOffers { get; private set; } = new();
        public Dictionary<int, List<AvailableTerm>> AvailableTerms { get; private set; } = new();
        private Dictionary<int, SuggestionStatus> SuggestionStatuses { get; } = new();

        // Slider fetch flags. Defaults preserve the non-slider (apartments list page) behavior, whose
        // transient ViewModel instance never calls InitializeForSliderAsync.
        private bool _fetchDatedOffers = true;
        private bool _fetchPublicOffers = false;
        private bool _fetchSuggestions = true;

        public long? ApartmentsCount { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public bool ApartmentsIsLoading { get; private set; } = false;
        public bool IsSuggestionsLoading { get; private set; } = false;
        public bool HasMore { get; private set; } = true;
        public string? Error { get; private set; }
        public bool IsMapView { get; private set; } = false;
        public bool IsSearch { get; private set; } = false;

        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string Adults { get; set; } = "";
        public string Children { get; set; } = "";

        public int? FilterMinPrice { get; private set; }
        public int? FilterMaxPrice { get; private set; }

        public int ScaleMinPrice { get; private set; }
        public int ScaleMaxPrice { get; private set; }
        public Guid SliderResetKey { get; private set; } = Guid.NewGuid();
        private int _suggestionsRunId = 0;
        private DateTime _lastNotifyTime = DateTime.MinValue;
        private readonly object _notifyLock = new();
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        public event Action? OnChange;

        public ApartmentsViewModel(
            IApartmentsService apartmentsService,
            IRentoomOfferService rentoomOfferService,
            IIdoOfferService idoOfferService,
            NavigationManager navManager,
            IAvailabilityFinderService2 availabilityFinder,
            IApartmentSearchFiltersService filterService,
            ReservationWorkflowTelemetry telemetry,
            GoogleAnalyticsService googleAnalytics,
            MediaCacheService mediaCache,
            IApartmentMediaCatalogService apartmentMediaCatalogService,
            IRouteLocalizationService routeService,
            ILogger<ApartmentsViewModel> logger)
        {
            _apartmentsService = apartmentsService;
            _rentoomOfferService = rentoomOfferService;
            _idoOfferService = idoOfferService;
            _navManager = navManager;
            _availabilityFinder = availabilityFinder;
            _filterService = filterService;
            _telemetry = telemetry;
            _googleAnalytics = googleAnalytics;
            _mediaCache = mediaCache;
            _apartmentMediaCatalogService = apartmentMediaCatalogService;
            _routeService = routeService;
            _logger = logger;
        }

        public int MinOfferPrice => ScaleMinPrice;
        public int MaxOfferPrice => ScaleMaxPrice;

        public PricingOffer? GetPricingOfferByObjectId(int objectId) => Offers.Find(o => o.ObjectId == objectId);
        public PublicApartmentOffer? GetPublicOfferByObjectId(int objectId) =>
            PublicOffers.TryGetValue(objectId, out var offer) ? offer : null;
        public SuggestionStatus GetSuggestionStatusByObjectId(int objectId) =>
            SuggestionStatuses.TryGetValue(objectId, out var status) ? status : SuggestionStatus.NotStarted;
        public IReadOnlyList<AvailableTerm>? GetSuggestionByObjectId(int objectId) => GetSuggestionsByObjectId(objectId);
        public IReadOnlyList<AvailableTerm>? GetSuggestionsByObjectId(int objectId) =>
            AvailableTerms.TryGetValue(objectId, out var terms) ? terms : null;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            CancelSuggestionsFetch();
            CancelMediaWarmOperations();
            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);
            string GetVal(string key) => query.TryGetValue(key, out var val) ? val.ToString() : "";

            // Use Query params if present, otherwise fallback to existing values (which might be set from Route Parameters)
            string s = GetVal("startDate");
            if (string.IsNullOrEmpty(s)) s = StartDate;

            string e = GetVal("endDate");
            if (string.IsNullOrEmpty(e)) e = EndDate;

            string a = GetVal("adults");
            if (string.IsNullOrEmpty(a)) a = Adults;

            string c = GetVal("children");
            if (string.IsNullOrEmpty(c)) c = Children;

            int? urlMin = int.TryParse(GetVal("minPrice"), out int minV) ? minV : null;
            int? urlMax = int.TryParse(GetVal("maxPrice"), out int maxV) ? maxV : null;
            int? urlUpsell = int.TryParse(GetVal("upsellId"), out int uV) ? uV : null;

            var urlLocs = GetVal("locations").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var urlAmes = GetVal("filters").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

            // Check if search parameters actually provide a valid search context
            StartDate = s; EndDate = e; Adults = a; Children = c;
            FilterMinPrice = urlMin;
            FilterMaxPrice = urlMax;
            IsSearch = !string.IsNullOrEmpty(StartDate) && !string.IsNullOrEmpty(EndDate);

            await GetApartmentsCount();

            _currentFilters = null;
            if (urlLocs.Any() || urlAmes.Any() || urlUpsell.HasValue)
            {
                _currentFilters = await ReconstructFiltersFromUrl(urlLocs, urlAmes);
                if (urlUpsell.HasValue)
                {
                    _currentFilters.ApartmentAddonFilter = new List<int> { urlUpsell.Value };
                }
            }

            Items.Clear(); Offers.Clear(); ResetSuggestionState(); _allMatchingOffers.Clear(); _matchingMetaItems.Clear(); _token = null;

            bool hasActiveFilters = IsSearch || (_currentFilters != null && (_currentFilters.ApartmentLocationsFilter?.Any() == true || _currentFilters.ApartmentAmenitiesFilter?.Any() == true || _currentFilters.ApartmentAddonFilter?.Any() == true));

            if (hasActiveFilters)
            {
                ApartmentsIsLoading = true; HasMore = false; NotifyStateChanged();

                var allItems = await GetAllApartments() ?? new List<ApartmentObject>();

                await FetchOffersAndSetScale(allItems, _currentFilters, updateScale: true);

                if (ScaleMaxPrice > 0)
                {
                    FilterMinPrice = urlMin ?? ScaleMinPrice;
                    FilterMaxPrice = urlMax ?? ScaleMaxPrice;
                }

                ApplyPriceFilterToItems(allItems);

                ApartmentsIsLoading = false;
                StartSuggestionsFetch(Items);
            }
            else
            {
                HasMore = true; ApartmentsIsLoading = false;
                ResetPriceScales();
                await LoadMoreAsync(ct);
            }

            _isInitialized = true;
            NotifyStateChanged();
        }

        public async Task InitializeForSliderAsync(bool showSuggestions = true, bool showPublicOffer = false, bool fetchDatedOffers = true, CancellationToken ct = default)
        {
            _fetchSuggestions = showSuggestions;
            _fetchPublicOffers = showPublicOffer;
            _fetchDatedOffers = fetchDatedOffers;

            CancelSuggestionsFetch();
            CancelMediaWarmOperations();

            var now = DateTime.Now;
            var dateFrom = DateOnly.FromDateTime(now);
            var dateTo = DateOnly.FromDateTime(
                (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday)
                    ? now.AddDays(2)
                    : now.AddDays(1));

            StartDate = dateFrom.ToString("yyyy-MM-dd");
            EndDate = dateTo.ToString("yyyy-MM-dd");
            Adults = "2";
            Children = "0";
            IsSearch = true;
            FilterMinPrice = null;
            FilterMaxPrice = null;
            _currentFilters = null;

            Items.Clear(); Offers.Clear(); PublicOffers.Clear(); ResetSuggestionState(); _allMatchingOffers.Clear(); _matchingMetaItems.Clear(); _token = null;

            await GetApartmentsCount();

            HasMore = true; ApartmentsIsLoading = false;
            ResetPriceScales();
            await LoadMoreAsync(ct);
            if (_fetchSuggestions)
            {
                StartSuggestionsFetch(Items);
            }

            _isInitialized = true;
            NotifyStateChanged();
        }

        public async Task HandleSearchAsync(Dictionary<string, string> query)
        {
            CancelSuggestionsFetch();
            CancelMediaWarmOperations();
            UpdateUrlParams(query);

            Items.Clear(); Offers.Clear(); ResetSuggestionState(); _allMatchingOffers.Clear(); _matchingMetaItems.Clear(); _token = null;
            HasMore = false; ApartmentsIsLoading = true; IsSearch = true;
            NotifyStateChanged();

            var allItems = await GetAllApartments() ?? new List<ApartmentObject>();

            await FetchOffersAndSetScale(allItems, _currentFilters, updateScale: true);

            FilterMinPrice = ScaleMinPrice;
            FilterMaxPrice = ScaleMaxPrice;
            SliderResetKey = Guid.NewGuid();

            ApplyPriceFilterToItems(allItems);
            TrackSearchResultsEvent("ApartmentSearchResultsLoaded");
            await TrackSearchResultsGaEventAsync("apartment_search_results");

            ApartmentsIsLoading = false;
            NotifyStateChanged();
            StartSuggestionsFetch(Items);
        }

        public async Task HandleFiltersChangedAsync((ApartmentFilters Filters, int MinPrice, int MaxPrice) data)
        {
            CancelSuggestionsFetch();
            CancelMediaWarmOperations();
            bool metaChanged = IsMetaFilterChanged(data.Filters);

            _currentFilters = data.Filters;
            FilterMinPrice = data.MinPrice;
            FilterMaxPrice = data.MaxPrice;

            if (metaChanged)
            {
                ResetSuggestionState();
            }

            ApartmentsIsLoading = true; HasMore = false;

            Items.Clear();
            Offers.Clear();
            NotifyStateChanged();

            var allItems = await GetAllApartments() ?? new List<ApartmentObject>();

            bool hasActiveMetaFilters = _currentFilters != null && (_currentFilters.ApartmentLocationsFilter?.Any() == true || _currentFilters.ApartmentAmenitiesFilter?.Any() == true || _currentFilters.ApartmentAddonFilter?.Any() == true);

            if (metaChanged || !hasActiveMetaFilters)
            {
                _allMatchingOffers.Clear();
                _matchingMetaItems.Clear();
                await FetchOffersAndSetScale(allItems, _currentFilters, updateScale: !hasActiveMetaFilters);
            }

            ApplyPriceFilterToItems(allItems);
            TrackSearchResultsEvent("ApartmentSearchFiltersApplied");
            await TrackSearchResultsGaEventAsync("apartment_filters_applied");

            ApartmentsIsLoading = false;
            NotifyStateChanged();

            if (metaChanged)
            {
                StartSuggestionsFetch(Items);
            }
        }

        private async Task FetchOffersAndSetScale(List<ApartmentObject> items, ApartmentFilters? filters, bool updateScale)
        {
            if (items == null || !items.Any()) { if (updateScale) ResetPriceScales(); return; }

            try
            {
                var fAdults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
                var fChildren = int.TryParse(Children, out var c) ? c : 0;
                var ids = items.Select(x => x.Id).ToList();

                var idoParams = new PricingOffersRequest
                {
                    ObjectIds = ids,
                    DateFrom = StartDate,
                    DateTo = EndDate,
                    NumberOfAdults = fAdults,
                    NumberOfBigChildren = fChildren
                };

                var queryObj = new RentoomQueryOffer
                {
                    IdoOfferParams = idoParams,
                    ApartmentFilterParams = filters ?? new ApartmentFilters(),
                    PriceFilter = null
                };

                var response = await _rentoomOfferService.getOfferWitFilter(queryObj);
                _allMatchingOffers = response?.PricingOffers ?? new List<PricingOffer>();
                _matchingMetaItems = response?.ApartmentObjects ?? new List<ApartmentObject>();

                if (updateScale)
                {
                    if (_allMatchingOffers.Any())
                    {
                        ScaleMinPrice = (int)_allMatchingOffers.Min(o => o.MinimalPrice);
                        ScaleMaxPrice = (int)_allMatchingOffers.Max(o => o.MinimalPrice);
                    }
                    else
                    {
                        ResetPriceScales();
                    }
                }
            }
            catch
            {
                if (updateScale) ResetPriceScales();
                _allMatchingOffers.Clear();
                _matchingMetaItems.Clear();
            }
        }

        private void ApplyPriceFilterToItems(List<ApartmentObject> allItems)
        {
            Offers.Clear();
            Items.Clear();

            int userMin = FilterMinPrice ?? ScaleMinPrice;
            int userMax = FilterMaxPrice ?? ScaleMaxPrice;

            decimal bMin = (decimal)userMin - 5;
            decimal bMax = (decimal)userMax + 5;

            var visibleOffers = _allMatchingOffers
                .Where(o => o.MinimalPrice >= bMin && o.MinimalPrice <= bMax)
                .ToList();

            Offers.AddRange(visibleOffers);

            var visibleOfferIds = visibleOffers.Select(o => o.ObjectId).ToHashSet();
            var allOfferIdsForMeta = _allMatchingOffers.Select(o => o.ObjectId).ToHashSet();

            bool hasActiveMetaFilters = _currentFilters != null && (_currentFilters.ApartmentLocationsFilter?.Any() == true || _currentFilters.ApartmentAmenitiesFilter?.Any() == true || _currentFilters.ApartmentAddonFilter?.Any() == true);
            var baseSet = hasActiveMetaFilters ? _matchingMetaItems : allItems;

            var group1 = baseSet.Where(a => visibleOfferIds.Contains(a.Id)).ToList();

            group1.Sort((a, b) => {
                var offerA = GetPricingOfferByObjectId(a.Id);
                var offerB = GetPricingOfferByObjectId(b.Id);
                if (offerA == null || offerB == null) return 0;
                return offerB.MinimalPrice.CompareTo(offerA.MinimalPrice);
            });

            var group2 = baseSet.Where(a => !allOfferIdsForMeta.Contains(a.Id)).ToList();

            Items.AddRange(group1);
            Items.AddRange(group2);
        }

        private bool IsMetaFilterChanged(ApartmentFilters? newFilters)
        {
            if (_currentFilters == null && newFilters == null) return false;

            var oldLocs = _currentFilters?.ApartmentLocationsFilter ?? new List<string>();
            var newLocs = newFilters?.ApartmentLocationsFilter ?? new List<string>();
            var oldAmes = _currentFilters?.ApartmentAmenitiesFilter ?? new List<int>();
            var newAmes = newFilters?.ApartmentAmenitiesFilter ?? new List<int>();
            var oldAddons = _currentFilters?.ApartmentAddonFilter ?? new List<int>();
            var newAddons = newFilters?.ApartmentAddonFilter ?? new List<int>();

            return !AreListsEqual(oldLocs, newLocs) || !AreListsEqual(oldAmes, newAmes) || !AreListsEqual(oldAddons, newAddons);
        }

        private bool AreListsEqual<T>(List<T>? list1, List<T>? list2)
        {
            if (list1 == null && list2 == null) return true;
            if (list1 == null || list2 == null) return false;
            if (list1.Count != list2.Count) return false;
            return !list1.Except(list2).Any() && !list2.Except(list1).Any();
        }

        public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested || ApartmentsIsLoading || !HasMore) return;

            if (!await _loadLock.WaitAsync(0)) return;

            var loadStopwatch = Stopwatch.StartNew();
            Task? mediaWarmTask = null;
            var requestedPageSize = PageSize;
            var continuationToken = _token;
            var returnedCount = 0;
            var visibleItemsAfterLoad = Items.Count;

            try
            {
                var remainingCount = GetRemainingApartmentCount();
                if (remainingCount.HasValue)
                {
                    if (remainingCount.Value <= 0)
                    {
                        HasMore = false;
                        return;
                    }
                }

                ApartmentsIsLoading = true;
                Error = null;
                NotifyStateChanged(force: true);

                requestedPageSize = remainingCount.HasValue
                    ? Math.Min(PageSize, remainingCount.Value)
                    : PageSize;
                continuationToken = _token;

                _logger.LogInformation(
                    "Apartment list load more started. RequestedPageSize={RequestedPageSize}, CurrentVisibleItems={CurrentVisibleItems}, ContinuationToken={ContinuationToken}, HasMore={HasMore}, IsSearch={IsSearch}",
                    requestedPageSize,
                    Items.Count,
                    continuationToken,
                    HasMore,
                    IsSearch);

                var page = await _apartmentsService.GetApartmentsByPageAsync(_token, top: requestedPageSize);
                returnedCount = page?.Items?.Count ?? 0;
                if (page?.Items is { Count: > 0 })
                {
                    Items.AddRange(page.Items);
                    _token = page.ContinuationToken;
                    HasMore = !string.IsNullOrEmpty(_token);

                    var remainingAfterLoad = GetRemainingApartmentCount();
                    if (remainingAfterLoad.HasValue && remainingAfterLoad.Value <= 0)
                    {
                        HasMore = false;
                    }

                    visibleItemsAfterLoad = Items.Count;
                    mediaWarmTask = StartWarmMediaCacheForItemsAsync(page.Items, cancellationToken);

                    if (IsSearch)
                    {
                        if (_fetchDatedOffers)
                        {
                            await FetchOffersForVisibleItems(page.Items);
                        }
                        if (_fetchPublicOffers)
                        {
                            await FetchPublicOffersForVisibleItems(page.Items);
                        }
                        if (_fetchSuggestions)
                        {
                            StartSuggestionsFetchForNewItems(page.Items);
                        }
                    }
                }
                else
                {
                    HasMore = false;
                    visibleItemsAfterLoad = Items.Count;
                }

                _logger.LogInformation(
                    "Apartment list load more completed. RequestedPageSize={RequestedPageSize}, ReturnedCount={ReturnedCount}, NextToken={NextToken}, HasMore={HasMore}, VisibleItems={VisibleItems}, ElapsedMs={ElapsedMs}",
                    requestedPageSize,
                    returnedCount,
                    _token,
                    HasMore,
                    visibleItemsAfterLoad,
                    loadStopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Apartment list load more canceled. RequestedPageSize={RequestedPageSize}, ContinuationToken={ContinuationToken}, VisibleItems={VisibleItems}, ElapsedMs={ElapsedMs}",
                    requestedPageSize,
                    continuationToken,
                    Items.Count,
                    loadStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                HasMore = false;
                _logger.LogWarning(ex,
                    "Apartment list load more failed. RequestedPageSize={RequestedPageSize}, ContinuationToken={ContinuationToken}, VisibleItems={VisibleItems}, ElapsedMs={ElapsedMs}",
                    requestedPageSize,
                    continuationToken,
                    Items.Count,
                    loadStopwatch.ElapsedMilliseconds);
            }
            finally
            {
                ApartmentsIsLoading = false;
                NotifyStateChanged(force: true);

                if (mediaWarmTask is { IsCompleted: false })
                {
                    _logger.LogInformation(
                        "Apartment list batch rendered before media warm completion. RequestedPageSize={RequestedPageSize}, ReturnedCount={ReturnedCount}, VisibleItems={VisibleItems}",
                        requestedPageSize,
                        returnedCount,
                        visibleItemsAfterLoad);
                }

                _loadLock.Release();
            }
        }

        private int? GetRemainingApartmentCount()
        {
            if (!ApartmentsCount.HasValue)
            {
                return null;
            }

            var remaining = ApartmentsCount.Value - Items.Count;
            if (remaining <= 0)
            {
                return 0;
            }

            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        private async Task FetchOffersForVisibleItems(IEnumerable<ApartmentObject> items)
        {
            try
            {
                var ids = items.Select(x => x.Id).ToList();
                var fAdults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
                var idoParams = new PricingOffersRequest
                {
                    ObjectIds = ids,
                    DateFrom = StartDate,
                    DateTo = EndDate,
                    NumberOfAdults = fAdults
                };
                var queryObj = new RentoomQueryOffer { IdoOfferParams = idoParams, PriceFilter = null };
                var response = await _rentoomOfferService.getOfferWitFilter(queryObj);

                if (response?.PricingOffers != null)
                {
                    foreach (var offer in response.PricingOffers)
                    {
                        var existing = Offers.FirstOrDefault(o => o.ObjectId == offer.ObjectId);
                        if (existing == null) Offers.Add(offer);
                    }
                }
            }
            catch { }
        }

        private async Task FetchPublicOffersForVisibleItems(IEnumerable<ApartmentObject> items)
        {
            try
            {
                var ids = items.Select(x => x.Id).Where(id => id > 0).ToList();
                if (ids.Count == 0)
                {
                    return;
                }

                var map = await _idoOfferService.GetPublicOffersAsync(ids);
                foreach (var kvp in map)
                {
                    PublicOffers[kvp.Key] = kvp.Value;
                }
            }
            catch { }
        }

        private Task StartWarmMediaCacheForItemsAsync(IEnumerable<ApartmentObject> items, CancellationToken cancellationToken)
        {
            var apartmentIds = items
                .Select(item => item.Id)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (apartmentIds.Count == 0)
            {
                return Task.CompletedTask;
            }

            var linkedCts = CreateMediaWarmLinkedTokenSource(cancellationToken);
            return WarmMediaCacheForItemsAsync(apartmentIds, linkedCts);
        }

        private async Task WarmMediaCacheForItemsAsync(IReadOnlyCollection<int> apartmentIds, CancellationTokenSource linkedCts)
        {
            var warmStopwatch = Stopwatch.StartNew();
            var apartmentIdsCsv = string.Join(",", apartmentIds);

            try
            {
                _logger.LogInformation(
                    "Apartment media warm started. ApartmentCount={ApartmentCount}, ApartmentIds={ApartmentIds}, TimeoutSeconds={TimeoutSeconds}",
                    apartmentIds.Count,
                    apartmentIdsCsv,
                    MediaWarmTimeout.TotalSeconds);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(linkedCts.Token);
                timeoutCts.CancelAfter(MediaWarmTimeout);

                var mediaByApartmentId = await _apartmentMediaCatalogService.GetApartmentMediaBatchAsync(apartmentIds, timeoutCts.Token);
                if (linkedCts.IsCancellationRequested)
                {
                    return;
                }

                _mediaCache.PrimeMediaBatch(mediaByApartmentId);

                _logger.LogInformation(
                    "Apartment media warm completed. ApartmentCount={ApartmentCount}, WarmedApartmentCount={WarmedApartmentCount}, ApartmentIds={ApartmentIds}, ElapsedMs={ElapsedMs}",
                    apartmentIds.Count,
                    mediaByApartmentId.Count,
                    apartmentIdsCsv,
                    warmStopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Apartment media warm canceled. ApartmentCount={ApartmentCount}, ApartmentIds={ApartmentIds}, ElapsedMs={ElapsedMs}",
                    apartmentIds.Count,
                    apartmentIdsCsv,
                    warmStopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Apartment media warm timed out. ApartmentCount={ApartmentCount}, ApartmentIds={ApartmentIds}, TimeoutSeconds={TimeoutSeconds}, ElapsedMs={ElapsedMs}",
                    apartmentIds.Count,
                    apartmentIdsCsv,
                    MediaWarmTimeout.TotalSeconds,
                    warmStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Apartment media warm failed. ApartmentCount={ApartmentCount}, ApartmentIds={ApartmentIds}, ElapsedMs={ElapsedMs}",
                    apartmentIds.Count,
                    apartmentIdsCsv,
                    warmStopwatch.ElapsedMilliseconds);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        private async Task FetchSuggestionsInBackground(IReadOnlyCollection<int> apartmentIds, CancellationToken ct, int runId)
        {
            if (apartmentIds.Count == 0)
            {
                SetSuggestionsLoading(runId, false);
                return;
            }

            try
            {
                await Task.Delay(200, ct);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(SuggestionsFetchTimeout);

                await FetchSuggestionsForApartmentIds(apartmentIds, timeoutCts.Token, runId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Apartment suggestions timed out after {TimeoutSeconds}s for {ApartmentCount} apartments in run {RunId}.",
                    SuggestionsFetchTimeout.TotalSeconds,
                    apartmentIds.Count,
                    runId);
                MarkSuggestionsFailed(runId, apartmentIds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Apartment suggestions failed for {ApartmentCount} apartments in run {RunId}.",
                    apartmentIds.Count,
                    runId);
                MarkSuggestionsFailed(runId, apartmentIds);
            }
            finally
            {
                SetSuggestionsLoading(runId, false);
            }
        }

        private async Task FetchSuggestionsForApartmentIds(IReadOnlyCollection<int> apartmentIds, CancellationToken ct, int runId)
        {
            if (string.IsNullOrEmpty(StartDate) || string.IsNullOrEmpty(EndDate)) return;
            if (apartmentIds.Count == 0) return;

            if (ct.IsCancellationRequested) return;

            var adults = int.TryParse(Adults, out var parsedAdults) && parsedAdults > 0 ? parsedAdults : 2;
            var children = int.TryParse(Children, out var parsedChildren) && parsedChildren > 0 ? parsedChildren : 0;

            var newSuggestions = await _availabilityFinder.FindAvailableTermsAsync(
                apartmentIds.ToList(),
                StartDate,
                EndDate,
                adults,
                children,
                ct);

            if (ct.IsCancellationRequested) return;

            if (!IsCurrentSuggestionsRun(runId))
            {
                return;
            }

            var suggestionsByApartmentId = newSuggestions.ToDictionary(
                result => result.ApartmentId,
                result => (result.AvailableTerms ?? new List<AvailableTerm>())
                    .Take(3)
                    .ToList());

            foreach (var apartmentId in apartmentIds)
            {
                AvailableTerms.Remove(apartmentId);

                if (suggestionsByApartmentId.TryGetValue(apartmentId, out var topTerms) && topTerms.Count > 0)
                {
                    AvailableTerms[apartmentId] = topTerms;
                    SuggestionStatuses[apartmentId] = SuggestionStatus.ResolvedWithTerms;
                    continue;
                }

                SuggestionStatuses[apartmentId] = SuggestionStatus.ResolvedNoTerms;
            }

            UpdateSuggestionsLoadingState();
            NotifyStateChanged();
        }

        private void StartSuggestionsFetch(IEnumerable<ApartmentObject> items)
        {
            if (!IsSearch)
            {
                CancelSuggestionsFetch();
                return;
            }

            var apartmentIds = items
                .Where(item => !Offers.Any(o => o.ObjectId == item.Id))
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            CancelSuggestionsFetch();
            if (apartmentIds.Count == 0)
            {
                UpdateSuggestionsLoadingState();
                return;
            }

            _suggestionsCts = new CancellationTokenSource();
            var runId = _suggestionsRunId;
            MarkSuggestionsLoading(runId, apartmentIds);
            _ = FetchSuggestionsInBackground(apartmentIds, _suggestionsCts.Token, runId);
        }

        /// <summary>
        /// Fetches availability suggestions for apartments loaded by a subsequent LoadMoreAsync page,
        /// without cancelling any suggestions fetch already running for previously loaded items.
        /// </summary>
        private void StartSuggestionsFetchForNewItems(IEnumerable<ApartmentObject> newItems)
        {
            if (!IsSearch) return;

            var apartmentIds = newItems
                .Where(item => !Offers.Any(o => o.ObjectId == item.Id))
                .Where(item => !SuggestionStatuses.ContainsKey(item.Id))
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            if (apartmentIds.Count == 0) return;

            var cts = new CancellationTokenSource();
            _incrementalSuggestionsCts.Add(cts);

            var runId = _suggestionsRunId;
            MarkSuggestionsLoading(runId, apartmentIds);

            _ = RunIncrementalSuggestionsFetch(apartmentIds, cts, runId);
        }

        private async Task RunIncrementalSuggestionsFetch(IReadOnlyCollection<int> apartmentIds, CancellationTokenSource cts, int runId)
        {
            try
            {
                await FetchSuggestionsInBackground(apartmentIds, cts.Token, runId);
            }
            finally
            {
                _incrementalSuggestionsCts.Remove(cts);
                cts.Dispose();
            }
        }

        private void CancelSuggestionsFetch()
        {
            _suggestionsCts?.Cancel();
            _suggestionsCts?.Dispose();
            _suggestionsCts = null;

            foreach (var cts in _incrementalSuggestionsCts)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _incrementalSuggestionsCts.Clear();

            _suggestionsRunId++;
            ResetLoadingSuggestionStatuses();
            UpdateSuggestionsLoadingState();
        }

        private void CancelMediaWarmOperations()
        {
            CancellationTokenSource currentCts;

            lock (_mediaWarmLock)
            {
                currentCts = _mediaWarmGenerationCts;
                _mediaWarmGenerationCts = new CancellationTokenSource();
            }

            currentCts.Cancel();
            currentCts.Dispose();
        }

        private CancellationTokenSource CreateMediaWarmLinkedTokenSource(CancellationToken cancellationToken)
        {
            lock (_mediaWarmLock)
            {
                return cancellationToken.CanBeCanceled
                    ? CancellationTokenSource.CreateLinkedTokenSource(_mediaWarmGenerationCts.Token, cancellationToken)
                    : CancellationTokenSource.CreateLinkedTokenSource(_mediaWarmGenerationCts.Token);
            }
        }

        private void SetSuggestionsLoading(int runId, bool isLoading)
        {
            if (runId != _suggestionsRunId) return;
            UpdateSuggestionsLoadingState();

            if (!isLoading)
            {
                NotifyStateChanged();
            }
        }

        private void ResetSuggestionState()
        {
            AvailableTerms.Clear();
            SuggestionStatuses.Clear();
            IsSuggestionsLoading = false;
        }

        private void ResetLoadingSuggestionStatuses()
        {
            foreach (var apartmentId in SuggestionStatuses
                         .Where(pair => pair.Value == SuggestionStatus.Loading)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                SuggestionStatuses[apartmentId] = SuggestionStatus.NotStarted;
                AvailableTerms.Remove(apartmentId);
            }
        }

        private void MarkSuggestionsLoading(int runId, IReadOnlyCollection<int> apartmentIds)
        {
            if (!IsCurrentSuggestionsRun(runId))
            {
                return;
            }

            foreach (var apartmentId in apartmentIds)
            {
                AvailableTerms.Remove(apartmentId);
                SuggestionStatuses[apartmentId] = SuggestionStatus.Loading;
            }

            UpdateSuggestionsLoadingState();
            NotifyStateChanged();
        }

        private void MarkSuggestionsFailed(int runId, IReadOnlyCollection<int> apartmentIds)
        {
            if (!IsCurrentSuggestionsRun(runId))
            {
                return;
            }

            foreach (var apartmentId in apartmentIds)
            {
                AvailableTerms.Remove(apartmentId);

                if (SuggestionStatuses.TryGetValue(apartmentId, out var status) && status == SuggestionStatus.Loading)
                {
                    SuggestionStatuses[apartmentId] = SuggestionStatus.Failed;
                }
            }

            UpdateSuggestionsLoadingState();
            NotifyStateChanged();
        }

        private bool IsCurrentSuggestionsRun(int runId) => runId == _suggestionsRunId;

        private void UpdateSuggestionsLoadingState()
        {
            IsSuggestionsLoading = SuggestionStatuses.Values.Any(status => status == SuggestionStatus.Loading);
        }

        private void UpdateUrlParams(Dictionary<string, string> query)
        {
            if (query.ContainsKey("startDate")) StartDate = query["startDate"];
            if (query.ContainsKey("endDate")) EndDate = query["endDate"];
            if (query.ContainsKey("adults")) Adults = query["adults"];
            if (query.ContainsKey("children")) Children = query["children"];

            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            queryParams["startDate"] = StartDate;
            queryParams["endDate"] = EndDate;
            queryParams["adults"] = Adults;
            queryParams["children"] = Children;
            queryParams.Remove("minPrice");
            queryParams.Remove("maxPrice");

            _navManager.NavigateTo($"{uri.AbsolutePath}?{queryParams}", forceLoad: false);
        }

        private async Task<ApartmentFilters> ReconstructFiltersFromUrl(HashSet<string> urlLocs, HashSet<string> urlAmes)
        {
            var filters = new ApartmentFilters();
            try
            {
                var allFilters = await _filterService.GetFiltersAsync();
                var locationNames = new List<string>();
                var amenityIds = new List<int>();

                var locDefs = allFilters.FirstOrDefault(f => f.id == "city-regions-filter")?.filtersDictionary.GetValueOrDefault("pl");
                if (locDefs != null)
                {
                    foreach (var id in urlLocs)
                    {
                        var match = locDefs.FirstOrDefault(l => l.id.ToString() == id);
                        if (match != null) locationNames.Add(match.name);
                    }
                }

                foreach (var idStr in urlAmes) if (int.TryParse(idStr, out int id)) amenityIds.Add(id);

                filters.ApartmentLocationsFilter = locationNames.Any() ? locationNames : null;
                filters.ApartmentAmenitiesFilter = amenityIds.Any() ? amenityIds : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reconstruct apartment filters from URL.");
            }
            return filters;
        }

        private async Task GetApartmentsCount()
        {
            IsLoading = true; NotifyStateChanged();
            try { ApartmentsCount = await _apartmentsService.GetApartmentsCountAsync(); }
            catch { ApartmentsCount = null; }
            finally { IsLoading = false; NotifyStateChanged(); }
        }

        private async Task<List<ApartmentObject>?> GetAllApartments()
        {
            try { var page = await _apartmentsService.GetAllApartmentsList(); return page?.Items?.ToList(); }
            catch { return null; }
        }

        private void TrackSearchResultsEvent(string eventName)
        {
            _telemetry.TrackEvent(
                eventName,
                new Dictionary<string, string?>
                {
                    ["Path"] = _navManager.ToAbsoluteUri(_navManager.Uri).AbsolutePath,
                    ["StartDate"] = StartDate,
                    ["EndDate"] = EndDate,
                    ["Adults"] = Adults,
                    ["Children"] = Children,
                    ["SelectedLocations"] = string.Join(",", _currentFilters?.ApartmentLocationsFilter ?? new List<string>()),
                    ["SelectedAmenities"] = string.Join(",", _currentFilters?.ApartmentAmenitiesFilter ?? new List<int>()),
                    ["SelectedAddons"] = string.Join(",", _currentFilters?.ApartmentAddonFilter ?? new List<int>())
                },
                new Dictionary<string, double>
                {
                    ["VisibleApartmentsCount"] = Items.Count,
                    ["VisibleOffersCount"] = Offers.Count,
                    ["MinPrice"] = FilterMinPrice ?? 0,
                    ["MaxPrice"] = FilterMaxPrice ?? 0
                });
        }

        private Task TrackSearchResultsGaEventAsync(string eventName)
        {
            return _googleAnalytics.TrackEventAsync(eventName, new Dictionary<string, object?>
            {
                ["page_path"] = _navManager.ToAbsoluteUri(_navManager.Uri).AbsolutePath,
                ["start_date"] = StartDate,
                ["end_date"] = EndDate,
                ["adults"] = Adults,
                ["children"] = Children,
                ["selected_locations"] = string.Join(",", _currentFilters?.ApartmentLocationsFilter ?? new List<string>()),
                ["selected_amenities"] = string.Join(",", _currentFilters?.ApartmentAmenitiesFilter ?? new List<int>()),
                ["selected_addons"] = string.Join(",", _currentFilters?.ApartmentAddonFilter ?? new List<int>()),
                ["visible_apartments_count"] = Items.Count,
                ["visible_offers_count"] = Offers.Count,
                ["min_price"] = FilterMinPrice ?? 0,
                ["max_price"] = FilterMaxPrice ?? 0
            });
        }

        public int? GetOfferLengthDays()
        {
            if (DateOnly.TryParse(StartDate, out var s) && DateOnly.TryParse(EndDate, out var e))
            {
                var days = e.DayNumber - s.DayNumber;
                return days > 0 ? days : null;
            }

            return null;
        }

        public async Task NavigateToApartmentAsync(int apartmentId, string? apartmentName, string listingSource, CancellationToken ct = default)
        {
            apartmentName ??= "apartament";

            if (!DateOnly.TryParse(StartDate, out var navigationStartDate))
            {
                navigationStartDate = DateOnly.FromDateTime(DateTime.Now);
            }

            if (!DateOnly.TryParse(EndDate, out var navigationEndDate))
            {
                navigationEndDate = navigationStartDate.AddDays(1);
            }

            var currentOffer = GetPricingOfferByObjectId(apartmentId);

            if (currentOffer == null)
            {
                var firstAvailableTerm = GetSuggestionsByObjectId(apartmentId)?.FirstOrDefault();

                if (firstAvailableTerm == null)
                {
                    try
                    {
                        var fAdults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
                        var fChildren = int.TryParse(Children, out var c) ? c : 0;

                        var suggestedTerm = await _availabilityFinder.FindAvailableTermsForApartmentAsync(
                            apartmentId,
                            StartDate,
                            EndDate,
                            fAdults,
                            fChildren);

                        firstAvailableTerm = suggestedTerm.AvailableTerms?.FirstOrDefault();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to find nearest available term for apartment {ApartmentId}.", apartmentId);
                    }
                }

                if (firstAvailableTerm != null
                    && DateOnly.TryParse(firstAvailableTerm.StartDate, out var suggestedStartDate)
                    && DateOnly.TryParse(firstAvailableTerm.EndDate, out var suggestedEndDate))
                {
                    navigationStartDate = suggestedStartDate;
                    navigationEndDate = suggestedEndDate;
                }
            }

            var adultsForNav = int.TryParse(Adults, out var adultsVal) && adultsVal > 0 ? adultsVal.ToString() : "2";
            var childrenForNav = int.TryParse(Children, out var childrenVal) && childrenVal >= 0 ? childrenVal.ToString() : "0";

            _telemetry.TrackEvent(
                "HomeApartmentClicked",
                new Dictionary<string, string?>
                {
                    ["ApartmentId"] = apartmentId.ToString(),
                    ["ApartmentName"] = apartmentName,
                    ["StartDate"] = navigationStartDate.ToString("yyyy-MM-dd"),
                    ["EndDate"] = navigationEndDate.ToString("yyyy-MM-dd"),
                    ["Adults"] = adultsForNav,
                    ["Children"] = childrenForNav,
                    ["ListingSource"] = listingSource,
                    ["HasOfferForRequestedDates"] = currentOffer != null ? "true" : "false"
                });

            await _googleAnalytics.TrackEventAsync("home_apartment_click", new Dictionary<string, object?>
            {
                ["apartment_id"] = apartmentId,
                ["apartment_name"] = apartmentName,
                ["listing_source"] = listingSource,
                ["start_date"] = navigationStartDate.ToString("yyyy-MM-dd"),
                ["end_date"] = navigationEndDate.ToString("yyyy-MM-dd"),
                ["adults"] = adultsForNav,
                ["children"] = childrenForNav,
                ["has_offer_for_requested_dates"] = currentOffer != null ? 1 : 0
            });

            var localizedBase = _routeService.GetLocalizedUrl("Apartments");
            _navManager.NavigateTo($"{localizedBase}/{apartmentId}/{apartmentName.ToSlug()}/{navigationStartDate:yyyy-MM-dd}/{navigationEndDate:yyyy-MM-dd}/{adultsForNav}/{childrenForNav}");
        }

        private void ResetPriceScales() { ScaleMinPrice = 0; ScaleMaxPrice = 0; FilterMinPrice = 0; FilterMaxPrice = 0; }
        public void ToggleView(bool isMap) { IsMapView = isMap; NotifyStateChanged(); }
        private void NotifyStateChanged(bool force = false)
        {
            lock (_notifyLock)
            {
                var now = DateTime.UtcNow;
                if (!force && (now - _lastNotifyTime).TotalMilliseconds < 100) return;
                _lastNotifyTime = now;
            }

            try
            {
                OnChange?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Apartments view model state notification failed.");
            }
        }
    }
}