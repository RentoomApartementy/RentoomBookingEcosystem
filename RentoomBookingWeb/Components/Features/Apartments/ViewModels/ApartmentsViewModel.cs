using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;

namespace RentoomBookingWeb.Components.Features.Apartments.ViewModels
{
    public class ApartmentsViewModel : IApartmentsViewModel
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IRentoomOfferService _rentoomOfferService;
        private readonly NavigationManager _navManager;

        private string? _token;
        private const int PageSize = 6;

        public List<ApartmentObject> Items { get; private set; } = new();
        public List<PricingOffer> Offers { get; private set; } = new();
        
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
            NavigationManager navManager)
        {
            _apartmentsService = apartmentsService;
            _rentoomOfferService = rentoomOfferService;
            _navManager = navManager;
        }

        public int MinOfferPrice => Offers.Count == 0 ? 0 : (int)Offers.Min(o => o.MinimalPrice);
        public int MaxOfferPrice => Offers.Count == 0 ? 0 : (int)Offers.Max(o => o.MinimalPrice);

        public PricingOffer? GetPricingOfferByObjectId(int objectId)
            => Offers.Find(o => o.ObjectId == objectId);

        public async Task InitializeAsync()
        {
            GetFiltersFromUrl();

            IsSearch = !string.IsNullOrEmpty(StartDate) || !string.IsNullOrEmpty(EndDate)
                        || !string.IsNullOrEmpty(Adults) || !string.IsNullOrEmpty(Children)
                        || !string.IsNullOrEmpty(Rooms);

            await GetApartmentsCount();

            if (IsSearch)
            {
                var query = new Dictionary<string, string>
                {
                    { "startDate", StartDate },
                    { "endDate", EndDate },
                    { "adults", Adults },
                    { "children", Children },
                    { "rooms", Rooms }
                };
                await HandleSearchAsync(query, updateUrl: false);
            }
            else
            {
                await LoadMoreAsync();
                var allItems = await GetAllApartments() ?? new List<ApartmentObject>();
                await GetFilteredOffers(allItems);
            }
        }

        public async Task LoadMoreAsync()
        {
            if (ApartmentsIsLoading || !HasMore) return;

            ApartmentsIsLoading = true;
            Error = null;
            NotifyStateChanged();

            try
            {
                var page = await _apartmentsService.GetApartmentsByPageAsync(_token, top: PageSize);
                if (page?.Items is { Count: > 0 })
                {
                    Items.AddRange(page.Items);
                    _token = page.ContinuationToken;
                    HasMore = !string.IsNullOrEmpty(_token);
                }
                else
                {
                    HasMore = false;
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                HasMore = false;
            }
            finally
            {
                ApartmentsIsLoading = false;
                NotifyStateChanged();
            }
        }

        public void ToggleView(bool isMap)
        {
            IsMapView = isMap;
            NotifyStateChanged();
        }

        public async Task HandleSearchAsync(Dictionary<string, string> query)
        {
            await HandleSearchAsync(query, updateUrl: true);
        }

        public async Task HandleFiltersChangedAsync((ApartmentFilters Filters, int MinPrice, int MaxPrice) data)
        {
            var filters = data.Filters;
            FilterMinPrice = data.MinPrice;
            FilterMaxPrice = data.MaxPrice;

            Items.Clear();
            Offers.Clear();
            _token = null;
            HasMore = false;
            ApartmentsIsLoading = true;
            NotifyStateChanged();

            try
            {
                var items = await GetAllApartments() ?? new List<ApartmentObject>();
                Items.AddRange(items);

                await GetFilteredOffers(items, filters, forceReset: false);

                SortItemsByOffers();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                ApartmentsIsLoading = false;
                NotifyStateChanged();
            }
        }

        private async Task HandleSearchAsync(Dictionary<string, string> query, bool updateUrl)
        {
            if (query.ContainsKey("startDate")) StartDate = query["startDate"];
            if (query.ContainsKey("endDate")) EndDate = query["endDate"];
            if (query.ContainsKey("adults")) Adults = query["adults"];
            if (query.ContainsKey("children")) Children = query["children"];
            if (query.ContainsKey("rooms")) Rooms = query["rooms"];


            if (updateUrl)
            {
                var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                queryParams["startDate"] = StartDate;
                queryParams["endDate"] = EndDate;
                queryParams["adults"] = Adults;
                queryParams["children"] = Children;
                queryParams["rooms"] = Rooms;
        
                queryParams.Remove("minPrice");
                queryParams.Remove("maxPrice");

                var newUri = $"{uri.AbsolutePath}?{queryParams}";
                _navManager.NavigateTo(newUri, forceLoad: false);
            }

            Items.Clear();
            Offers.Clear();
            _token = null;
            HasMore = false;

            await GetApartmentsCount();
    
            ApartmentsIsLoading = true;
            NotifyStateChanged();

            var fetchedItems = await GetAllApartments() ?? new List<ApartmentObject>();
            Items.AddRange(fetchedItems);

            await GetFilteredOffers(fetchedItems, explicitFilters: null, forceReset: true);
    
            SortItemsByOffers();

            ApartmentsIsLoading = false;
            IsSearch = true;
            
            SliderResetKey = Guid.NewGuid();
    
            NotifyStateChanged();
        }

        private async Task GetApartmentsCount()
        {
            IsLoading = true;
            NotifyStateChanged();
            try
            {
                ApartmentsCount = await _apartmentsService.GetApartmentsCountAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                ApartmentsCount = null;
            }
            finally
            {
                IsLoading = false;
                NotifyStateChanged();
            }
        }

        private async Task<List<ApartmentObject>?> GetAllApartments()
        {
            try
            {
                var page = await _apartmentsService.GetAllApartmentsList();
                var items = new List<ApartmentObject>();
                if (page?.Items is { Count: > 0 })
                {
                    items.AddRange(page.Items);
                }
                return items;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        private async Task GetFilteredOffers(List<ApartmentObject> items, ApartmentFilters? explicitFilters = null, bool forceReset = false)
        {
            try
            {
                if (items == null || !items.Any())
                {
                    ScaleMinPrice = 0;
                    ScaleMaxPrice = 0;
                    FilterMinPrice = 0;
                    FilterMaxPrice = 0;
                    return;
                }

                List<int> ids = items.Select(a => a.Id).ToList();

                var idoParams = new PricingOffersRequest
                {
                    ObjectIds = ids,
                    DateFrom = StartDate,
                    DateTo = EndDate,
                    NumberOfAdults = int.TryParse(Adults, out var adults) ? adults : 0,
                    NumberOfBigChildren = int.TryParse(Children, out var children) ? children : 0,
                };

                var filters = new RentoomQueryOffer
                {
                    IdoOfferParams = idoParams,
                    ApartmentFilterParams = explicitFilters ?? new ApartmentFilters(),
                    PriceFilter = null
                };

                var filteredOffers = await _rentoomOfferService.getOfferWitFilter(filters);

                if (filteredOffers?.PricingOffers != null)
                {
                    var allOffers = filteredOffers.PricingOffers;

                    if (allOffers.Any())
                    {
                        var newScaleMin = (int)allOffers.Min(o => o.MinimalPrice) - 1;
                        var newScaleMax = (int)allOffers.Max(o => o.MinimalPrice) + 1;
                        
                        ScaleMinPrice = newScaleMin;
                        ScaleMaxPrice = newScaleMax;

                        if (forceReset)
                        {
                            FilterMinPrice = newScaleMin;
                            FilterMaxPrice = newScaleMax;
                        }
                        else
                        {
                            if (FilterMinPrice == null) FilterMinPrice = newScaleMin;
                            if (FilterMaxPrice == null) FilterMaxPrice = newScaleMax;

                            if (FilterMinPrice < newScaleMin) FilterMinPrice = newScaleMin;
                            if (FilterMaxPrice > newScaleMax) FilterMaxPrice = newScaleMax;
                            
                            if (FilterMinPrice > FilterMaxPrice)
                            {
                                FilterMinPrice = newScaleMin;
                                FilterMaxPrice = newScaleMax;
                            }
                        }
                    }
                    else
                    {
                        ScaleMinPrice = 0;
                        ScaleMaxPrice = 0;
                        FilterMinPrice = 0;
                        FilterMaxPrice = 0;
                    }

                    var minLimit = FilterMinPrice ?? 0;
                    var maxLimit = (FilterMaxPrice.HasValue && FilterMaxPrice > 0) ? FilterMaxPrice.Value : int.MaxValue;

                    var displayedOffers = allOffers
                        .Where(o => o.MinimalPrice >= minLimit && o.MinimalPrice <= maxLimit)
                        .ToList();

                    Offers.AddRange(displayedOffers);
                }
                else
                {
                    ScaleMinPrice = 0;
                    ScaleMaxPrice = 0;
                    FilterMinPrice = 0;
                    FilterMaxPrice = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetFilteredOffers: {ex.Message}");
            }
        }

        private void GetFiltersFromUrl()
        {
            var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
            var query = QueryHelpers.ParseQuery(uri.Query);

            StartDate = query.TryGetValue("startDate", out var s) ? s.ToString() : "";
            EndDate = query.TryGetValue("endDate", out var e) ? e.ToString() : "";
            Adults = query.TryGetValue("adults", out var a) ? a.ToString() : "";
            Children = query.TryGetValue("children", out var c) ? c.ToString() : "";
            Rooms = query.TryGetValue("rooms", out var r) ? r.ToString() : "";
            
            if (int.TryParse(query.TryGetValue("minPrice", out var min) ? min.ToString() : null, out int minVal))
            {
                FilterMinPrice = minVal;
            }

            if (int.TryParse(query.TryGetValue("maxPrice", out var max) ? max.ToString() : null, out int maxVal))
            {
                FilterMaxPrice = maxVal;
            }
        }

        private void SortItemsByOffers()
        {
            Items.Sort((a, b) =>
            {
                var offerA = GetPricingOfferByObjectId(a.Id);
                var offerB = GetPricingOfferByObjectId(b.Id);
        
                if (offerA != null && offerB == null) return -1;
        
                if (offerA == null && offerB != null) return 1;

                if (offerA != null && offerB != null)
                {
                    return offerB.MinimalPrice.CompareTo(offerA.MinimalPrice);
                }

                return 0; 
            });
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}