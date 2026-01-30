using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBookingWeb.Models;
using RentoomBookingWeb.Services;

namespace RentoomBookingWeb.Components.Features.Apartments.ViewModels
{
    public class ApartmentsViewModel : IApartmentsViewModel
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IRentoomOfferService _rentoomOfferService;
        private readonly NavigationManager _navManager;
        private readonly IAvailabilityFinderService _availabilityFinder;
        private readonly IApartmentSearchFiltersService _filterService;

        private string? _token;
        private const int PageSize = 6;
        private bool _isInitialized = false;
        private ApartmentFilters? _currentFilters = null;
        
        private List<PricingOffer> _allMatchingOffers = new();

        public List<ApartmentObject> Items { get; private set; } = new();
        public List<PricingOffer> Offers { get; private set; } = new();
        public Dictionary<int, AvailableTerm> AvailableTerms { get; private set; } = new();

        public long? ApartmentsCount { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public bool ApartmentsIsLoading { get; private set; } = false;
        public bool HasMore { get; private set; } = true;
        public string? Error { get; private set; }
        public bool IsMapView { get; private set; } = false;
        public bool IsSearch { get; private set; } = false;

        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string Adults { get; set; } = "";
        public string Children { get; set; } = "";
        public string Rooms { get; set; } = "";

        public int? FilterMinPrice { get; private set; }
        public int? FilterMaxPrice { get; private set; }
        
        public int ScaleMinPrice { get; private set; }
        public int ScaleMaxPrice { get; private set; }
        public Guid SliderResetKey { get; private set; } = Guid.NewGuid();

        public event Action? OnChange;

        public ApartmentsViewModel(
            IApartmentsService apartmentsService,
            IRentoomOfferService rentoomOfferService,
            NavigationManager navManager,
            IAvailabilityFinderService availabilityFinder,
            IApartmentSearchFiltersService filterService)
        {
            _apartmentsService = apartmentsService;
            _rentoomOfferService = rentoomOfferService;
            _navManager = navManager;
            _availabilityFinder = availabilityFinder;
            _filterService = filterService;
        }

        public int MinOfferPrice => ScaleMinPrice;
        public int MaxOfferPrice => ScaleMaxPrice;

        public PricingOffer? GetPricingOfferByObjectId(int objectId) => Offers.Find(o => o.ObjectId == objectId);
        public AvailableTerm? GetSuggestionByObjectId(int objectId) => AvailableTerms.TryGetValue(objectId, out var term) ? term : null;

        public async Task InitializeAsync()
        {
            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);
            string GetVal(string key) => query.TryGetValue(key, out var val) ? val.ToString() : "";

            string s = GetVal("startDate");
            string e = GetVal("endDate");
            string a = GetVal("adults");
            string c = GetVal("children");
            string r = GetVal("rooms");
            
            int? urlMin = int.TryParse(GetVal("minPrice"), out int minV) ? minV : null;
            int? urlMax = int.TryParse(GetVal("maxPrice"), out int maxV) ? maxV : null;

            var urlLocs = GetVal("locations").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var urlAmes = GetVal("filters").Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

            bool filtersChanged = s != StartDate || e != EndDate || a != Adults || urlMin != FilterMinPrice || urlMax != FilterMaxPrice;

            if (_isInitialized && !filtersChanged && Items.Any())
            {
                NotifyStateChanged();
                return;
            }

            StartDate = s; EndDate = e; Adults = a; Children = c; Rooms = r;
            FilterMinPrice = urlMin;
            FilterMaxPrice = urlMax;
            IsSearch = !string.IsNullOrEmpty(StartDate) || !string.IsNullOrEmpty(EndDate);

            await GetApartmentsCount();

            _currentFilters = null;
            if (urlLocs.Any() || urlAmes.Any())
            {
                _currentFilters = await ReconstructFiltersFromUrl(urlLocs, urlAmes);
            }

            Items.Clear(); Offers.Clear(); AvailableTerms.Clear(); _allMatchingOffers.Clear(); _token = null;
            
            bool hasActiveFilters = IsSearch || _currentFilters != null;

            if (hasActiveFilters)
            {
                ApartmentsIsLoading = true; HasMore = false; NotifyStateChanged();

                var allItems = await GetAllApartments() ?? new List<ApartmentObject>();
                
                await FetchAndCalculateScale(allItems, _currentFilters);
                
                if (ScaleMaxPrice > 0)
                {
                    FilterMinPrice = urlMin ?? ScaleMinPrice;
                    FilterMaxPrice = urlMax ?? ScaleMaxPrice;
                }

                ApplyPriceFilterToItems(allItems);
                
                ApartmentsIsLoading = false;
                _ = FetchSuggestionsInBackground(Items);
            }
            else
            {
                HasMore = true; ApartmentsIsLoading = false;
                ResetPriceScales();
                await LoadMoreAsync();
                if (Items.Any()) _ = FetchSuggestionsInBackground(Items.Take(PageSize));
            }
            
            _isInitialized = true;
            NotifyStateChanged();
        }

        public async Task HandleSearchAsync(Dictionary<string, string> query)
        {
            UpdateUrlParams(query);
            
            Items.Clear(); Offers.Clear(); AvailableTerms.Clear(); _allMatchingOffers.Clear(); _token = null; 
            HasMore = false; ApartmentsIsLoading = true; IsSearch = true;
            NotifyStateChanged();

            var allItems = await GetAllApartments() ?? new List<ApartmentObject>();
            
            await FetchAndCalculateScale(allItems, _currentFilters);
            
            FilterMinPrice = ScaleMinPrice;
            FilterMaxPrice = ScaleMaxPrice;
            SliderResetKey = Guid.NewGuid();

            ApplyPriceFilterToItems(allItems);

            ApartmentsIsLoading = false;
            NotifyStateChanged();
            _ = FetchSuggestionsInBackground(Items);
        }

        public async Task HandleFiltersChangedAsync((ApartmentFilters Filters, int MinPrice, int MaxPrice) data)
        {
            bool metaChanged = IsMetaFilterChanged(data.Filters);

            _currentFilters = data.Filters;
            FilterMinPrice = data.MinPrice;
            FilterMaxPrice = data.MaxPrice;

            ApartmentsIsLoading = true; HasMore = false; NotifyStateChanged();

            var allItems = await GetAllApartments() ?? new List<ApartmentObject>();

            if (metaChanged)
            {
                Items.Clear(); Offers.Clear(); _allMatchingOffers.Clear();
                
                await FetchAndCalculateScale(allItems, _currentFilters);

                if (ScaleMaxPrice > 0 && (FilterMaxPrice <= 0 || FilterMaxPrice < ScaleMinPrice))
                {
                    FilterMinPrice = ScaleMinPrice;
                    FilterMaxPrice = ScaleMaxPrice;
                }
                
                SliderResetKey = Guid.NewGuid();
            }

            ApplyPriceFilterToItems(allItems);

            ApartmentsIsLoading = false;
            NotifyStateChanged();
            
            if (metaChanged) _ = FetchSuggestionsInBackground(Items);
        }

        private async Task FetchAndCalculateScale(List<ApartmentObject> items, ApartmentFilters? filters)
        {
            if (items == null || !items.Any()) { ResetPriceScales(); return; }

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
            catch 
            {
                ResetPriceScales();
                _allMatchingOffers.Clear();
            }
        }

        private void ApplyPriceFilterToItems(List<ApartmentObject> allItems)
        {
            Offers.Clear();
            Items.Clear();

            int userMin = FilterMinPrice ?? ScaleMinPrice;
            int userMax = FilterMaxPrice ?? ScaleMaxPrice;

            int bufferedMin = Math.Max(0, userMin - 10);
            int bufferedMax = userMax + 10;

            var visibleOffers = _allMatchingOffers
                .Where(o => o.MinimalPrice >= bufferedMin && o.MinimalPrice <= bufferedMax)
                .ToList();

            Offers.AddRange(visibleOffers);

            var outOfRangeOfferIds = _allMatchingOffers
                .Where(o => o.MinimalPrice < bufferedMin || o.MinimalPrice > bufferedMax)
                .Select(o => o.ObjectId)
                .ToHashSet();

            var itemsToShow = allItems.Where(a => !outOfRangeOfferIds.Contains(a.Id)).ToList();
            
            Items.AddRange(itemsToShow);
            SortItemsByOffers();
        }

        private bool IsMetaFilterChanged(ApartmentFilters? newFilters)
        {
            if (_currentFilters == null && newFilters == null) return false;
            if (_currentFilters == null || newFilters == null) return true;

            bool locChanged = !AreListsEqual(_currentFilters.ApartmentLocationsFilter, newFilters.ApartmentLocationsFilter);
            bool ameChanged = !AreListsEqual(_currentFilters.ApartmentAmenitiesFilter, newFilters.ApartmentAmenitiesFilter);
            
            return locChanged || ameChanged;
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
            ApartmentsIsLoading = true; Error = null; NotifyStateChanged();

            try
            {
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
            finally { ApartmentsIsLoading = false; NotifyStateChanged(); }
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

        private void SortItemsByOffers()
        {
             Items.Sort((a, b) => {
                var offerA = GetPricingOfferByObjectId(a.Id);
                var offerB = GetPricingOfferByObjectId(b.Id);
                
                if (offerA != null && offerB == null) return -1;
                if (offerA == null && offerB != null) return 1;
                if (offerA != null && offerB != null) return offerB.MinimalPrice.CompareTo(offerA.MinimalPrice);
                
                return 0; 
            });
        }

        private async Task FetchSuggestionsInBackground(IEnumerable<ApartmentObject> items)
        {
            try { await Task.Delay(200); await FetchSuggestionsForItemsWithoutOffers(items); }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        private async Task FetchSuggestionsForItemsWithoutOffers(IEnumerable<ApartmentObject> itemsToCheck)
        {
            if (string.IsNullOrEmpty(StartDate) || string.IsNullOrEmpty(EndDate)) return;
            var orderedIds = itemsToCheck.Where(item => !Offers.Any(o => o.ObjectId == item.Id)).Select(item => item.Id).ToList();
            if (!orderedIds.Any()) return;
            
            var chunks = orderedIds.Chunk(10).ToList();
            for (int i = 0; i < chunks.Count; i++)
            {
                var newSuggestions = await _availabilityFinder.FindNextAvailableTermsAsync(chunks[i].ToList(), StartDate, EndDate, int.TryParse(Adults, out var a) ? a : 2, int.TryParse(Children, out var c) ? c : 1);
                foreach (var kvp in newSuggestions) AvailableTerms[kvp.Key] = kvp.Value;
                NotifyStateChanged();
                await Task.Delay(500);
            }
        }

        private void UpdateUrlParams(Dictionary<string, string> query)
        {
            if (query.ContainsKey("startDate")) StartDate = query["startDate"];
            if (query.ContainsKey("endDate")) EndDate = query["endDate"];
            if (query.ContainsKey("adults")) Adults = query["adults"];
            if (query.ContainsKey("children")) Children = query["children"];
            if (query.ContainsKey("rooms")) Rooms = query["rooms"];

            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            queryParams["startDate"] = StartDate;
            queryParams["endDate"] = EndDate;
            queryParams["adults"] = Adults;
            queryParams["children"] = Children;
            queryParams["rooms"] = Rooms;
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
        
        private void ResetPriceScales() { ScaleMinPrice = 0; ScaleMaxPrice = 0; FilterMinPrice = 0; FilterMaxPrice = 0; }
        public void ToggleView(bool isMap) { IsMapView = isMap; NotifyStateChanged(); }
        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}