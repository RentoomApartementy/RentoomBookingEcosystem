using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using RentoomBooking.SharedClasses.Models.AvailableTerms;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBookingWeb.Services;

namespace RentoomBookingWeb.Components.Features.Apartments.ViewModels
{
    public class ApartmentsViewModel : IApartmentsViewModel
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IRentoomOfferService _rentoomOfferService;
        private readonly NavigationManager _navManager;
        private readonly IAvailabilityFinderService2 _availabilityFinder;
        private readonly IApartmentSearchFiltersService _filterService;
        private readonly ReservationWorkflowTelemetry _telemetry;
        private readonly GoogleAnalyticsService _googleAnalytics;

        private string? _token;
        private const int PageSize = 6;
        private bool _isInitialized = false;
        private ApartmentFilters? _currentFilters = null;
        private CancellationTokenSource? _suggestionsCts;
        
        private List<PricingOffer> _allMatchingOffers = new();
        private List<ApartmentObject> _matchingMetaItems = new();

        public List<ApartmentObject> Items { get; private set; } = new();
        public List<PricingOffer> Offers { get; private set; } = new();
        public Dictionary<int, List<AvailableTerm>> AvailableTerms { get; private set; } = new();

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
            NavigationManager navManager,
            IAvailabilityFinderService2 availabilityFinder,
            IApartmentSearchFiltersService filterService,
            ReservationWorkflowTelemetry telemetry,
            GoogleAnalyticsService googleAnalytics)
        {
            _apartmentsService = apartmentsService;
            _rentoomOfferService = rentoomOfferService;
            _navManager = navManager;
            _availabilityFinder = availabilityFinder;
            _filterService = filterService;
            _telemetry = telemetry;
            _googleAnalytics = googleAnalytics;
        }

        public int MinOfferPrice => ScaleMinPrice;
        public int MaxOfferPrice => ScaleMaxPrice;

        public PricingOffer? GetPricingOfferByObjectId(int objectId) => Offers.Find(o => o.ObjectId == objectId);
        public IReadOnlyList<AvailableTerm>? GetSuggestionByObjectId(int objectId) => GetSuggestionsByObjectId(objectId);
        public IReadOnlyList<AvailableTerm>? GetSuggestionsByObjectId(int objectId) =>
            AvailableTerms.TryGetValue(objectId, out var terms) ? terms : null;

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            CancelSuggestionsFetch();
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

            Items.Clear(); Offers.Clear(); AvailableTerms.Clear(); _allMatchingOffers.Clear(); _matchingMetaItems.Clear(); _token = null;
            
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
                await LoadMoreAsync();
                if (Items.Any()) 
                {
                    StartSuggestionsFetch(Items.Take(PageSize));
                }
            }
            
            _isInitialized = true;
            NotifyStateChanged();
        }

        public async Task HandleSearchAsync(Dictionary<string, string> query)
        {
            CancelSuggestionsFetch();
            UpdateUrlParams(query);
            
            Items.Clear(); Offers.Clear(); AvailableTerms.Clear(); _allMatchingOffers.Clear(); _matchingMetaItems.Clear(); _token = null; 
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
            bool metaChanged = IsMetaFilterChanged(data.Filters);

            _currentFilters = data.Filters;
            FilterMinPrice = data.MinPrice;
            FilterMaxPrice = data.MaxPrice;

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
            if (items == null || !items.Any()) { if(updateScale) ResetPriceScales(); return; }

            try
            {
                var fAdults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
                var fChildren = int.TryParse(Children, out var c) ? c : 0;
                var ids = items.Select(x => x.Id).ToList();

                var idoParams = new PricingOffersRequest {
                    ObjectIds = ids, DateFrom = StartDate, DateTo = EndDate,
                    NumberOfAdults = fAdults, NumberOfBigChildren = fChildren
                };

                var queryObj = new RentoomQueryOffer { 
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
                if(updateScale) ResetPriceScales();
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

        public async Task LoadMoreAsync()
        {
            if (ApartmentsIsLoading || !HasMore) return;
            
            if (!await _loadLock.WaitAsync(0)) return;

            try
            {
                ApartmentsIsLoading = true; 
                Error = null; 
                NotifyStateChanged();

                var page = await _apartmentsService.GetApartmentsByPageAsync(_token, top: PageSize);
                if (page?.Items is { Count: > 0 })
                {
                    Items.AddRange(page.Items);
                    _token = page.ContinuationToken;
                    HasMore = !string.IsNullOrEmpty(_token);
                    
                    await FetchOffersForVisibleItems(page.Items); 
                }
                else { HasMore = false; }
            }
            catch (Exception ex) { Error = ex.Message; HasMore = false; }
            finally 
            { 
                ApartmentsIsLoading = false; 
                NotifyStateChanged(); 
                _loadLock.Release();
            }
        }

        private async Task FetchOffersForVisibleItems(IEnumerable<ApartmentObject> items)
        {
            try
            {
                var ids = items.Select(x => x.Id).ToList();
                var fAdults = int.TryParse(Adults, out var a) && a > 0 ? a : 2;
                var idoParams = new PricingOffersRequest {
                    ObjectIds = ids, DateFrom = StartDate, DateTo = EndDate,
                    NumberOfAdults = fAdults
                };
                var queryObj = new RentoomQueryOffer { IdoOfferParams = idoParams, PriceFilter = null };
                var response = await _rentoomOfferService.getOfferWitFilter(queryObj);
                
                if (response?.PricingOffers != null)
                {
                    foreach(var offer in response.PricingOffers)
                    {
                        var existing = Offers.FirstOrDefault(o => o.ObjectId == offer.ObjectId);
                        if (existing == null) Offers.Add(offer);
                    }
                }
            }
            catch { }
        }

        private async Task FetchSuggestionsInBackground(IEnumerable<ApartmentObject> items, CancellationToken ct, int runId)
        {
            SetSuggestionsLoading(runId, true);
            try { await Task.Delay(200, ct); await FetchSuggestionsForItemsWithoutOffers(items, ct); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
            finally { SetSuggestionsLoading(runId, false); }
        }

        private async Task FetchSuggestionsForItemsWithoutOffers(IEnumerable<ApartmentObject> itemsToCheck, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(StartDate) || string.IsNullOrEmpty(EndDate)) return;
            var orderedIds = itemsToCheck.Where(item => !Offers.Any(o => o.ObjectId == item.Id)).Select(item => item.Id).ToList();
            if (!orderedIds.Any()) return;

            if (ct.IsCancellationRequested) return;

            var adults = int.TryParse(Adults, out var parsedAdults) && parsedAdults > 0 ? parsedAdults : 2;
            var children = int.TryParse(Children, out var parsedChildren) && parsedChildren > 0 ? parsedChildren : 0;

            var newSuggestions = await _availabilityFinder.FindAvailableTermsAsync(
                orderedIds,
                StartDate,
                EndDate,
                adults,
                children);

            if (ct.IsCancellationRequested) return;

            foreach (var apartmentId in orderedIds)
            {
                AvailableTerms.Remove(apartmentId);
            }

            foreach (var result in newSuggestions)
            {
                var topTerms = (result.AvailableTerms ?? new List<AvailableTerm>())
                    .Take(3)
                    .ToList();

                if (topTerms.Count > 0)
                {
                    AvailableTerms[result.ApartmentId] = topTerms;
                }
            }

            NotifyStateChanged();
        }

        private void StartSuggestionsFetch(IEnumerable<ApartmentObject> items)
        {
            CancelSuggestionsFetch();
            _suggestionsCts = new CancellationTokenSource();
            var runId = _suggestionsRunId;
            _ = FetchSuggestionsInBackground(items, _suggestionsCts.Token, runId);
        }

        private void CancelSuggestionsFetch()
        {
            _suggestionsCts?.Cancel();
            _suggestionsCts?.Dispose();
            _suggestionsCts = null;
            _suggestionsRunId++;

            if (IsSuggestionsLoading)
            {
                IsSuggestionsLoading = false;
                NotifyStateChanged();
            }
        }

        private void SetSuggestionsLoading(int runId, bool isLoading)
        {
            if (runId != _suggestionsRunId) return;
            if (IsSuggestionsLoading == isLoading) return;
            IsSuggestionsLoading = isLoading;
            NotifyStateChanged();
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
                    foreach(var id in urlLocs)
                    {
                        var match = locDefs.FirstOrDefault(l => l.id.ToString() == id);
                        if (match != null) locationNames.Add(match.name);
                    }
                }

                foreach(var idStr in urlAmes) if (int.TryParse(idStr, out int id)) amenityIds.Add(id);

                filters.ApartmentLocationsFilter = locationNames.Any() ? locationNames : null;
                filters.ApartmentAmenitiesFilter = amenityIds.Any() ? amenityIds : null;
            }
            catch (Exception ex) { Console.WriteLine(ex); }
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
        
        private void ResetPriceScales() { ScaleMinPrice = 0; ScaleMaxPrice = 0; FilterMinPrice = 0; FilterMaxPrice = 0; }
        public void ToggleView(bool isMap) { IsMapView = isMap; NotifyStateChanged(); }
        private void NotifyStateChanged()
        {
            lock (_notifyLock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastNotifyTime).TotalMilliseconds < 100) return;
                _lastNotifyTime = now;
            }
            
            try 
            {
                OnChange?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in NotifyStateChanged: {ex.Message}");
            }
        }
    }
}
