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
                await GetFilteredOffers();
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

        public async Task HandleFiltersChangedAsync(ApartmentFilters filters)
        {
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

                List<int> ids = items.Select(a => a.Id).ToList();

                var idoParams = new PricingOffersRequest
                {
                    ObjectIds = ids,
                    DateFrom = StartDate,
                    DateTo = EndDate,
                    NumberOfAdults = int.TryParse(Adults, out var adults) ? adults : 0,
                    NumberOfBigChildren = int.TryParse(Children, out var children) ? children : 0,
                };

                var rentoomFilters = new RentoomQueryOffer
                {
                    IdoOfferParams = idoParams,
                    ApartmentFilterParams = filters
                };

                var filteredOffers = await _rentoomOfferService.getOfferWitFilter(rentoomFilters);

                if (filteredOffers?.PricingOffers != null)
                {
                    Offers.AddRange(filteredOffers.PricingOffers);
                }

                SortItemsByOffers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd przy filtrowaniu: {ex.Message}");
            }
            finally 
            {
                ApartmentsIsLoading = false;
                NotifyStateChanged();
            }
        }


        private async Task HandleSearchAsync(Dictionary<string, string> query, bool updateUrl)
        {
            StartDate = query.GetValueOrDefault("startDate", "");
            EndDate = query.GetValueOrDefault("endDate", "");
            Adults = query.GetValueOrDefault("adults", "");
            Children = query.GetValueOrDefault("children", "");
            Rooms = query.GetValueOrDefault("rooms", "");

            if (updateUrl)
            {
                var uri = _navManager.ToAbsoluteUri(_navManager.Uri);
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                queryParams["startDate"] = StartDate;
                queryParams["endDate"] = EndDate;
                queryParams["adults"] = Adults;
                queryParams["children"] = Children;
                queryParams["rooms"] = Rooms;

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

            Items.AddRange(await GetAllApartments() ?? new List<ApartmentObject>());

            await GetFilteredOffers();

            SortItemsByOffers();

            ApartmentsIsLoading = false;
            IsSearch = true;
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
                Console.WriteLine($"Wystąpił błąd: {ex.Message}");
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

        private async Task GetFilteredOffers()
        {
            try
            {
                var items = await GetAllApartments() ?? new List<ApartmentObject>();

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
                    ApartmentFilterParams = new ApartmentFilters()
                };

                var filteredOffers = await _rentoomOfferService.getOfferWitFilter(filters);
                if (filteredOffers?.PricingOffers != null)
                {
                    Offers.AddRange(filteredOffers.PricingOffers);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Wystąpił błąd: {ex.Message}");
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
        }

        private void SortItemsByOffers()
        {
            Items.Sort((a, b) =>
            {
                var offerA = GetPricingOfferByObjectId(a.Id);
                var offerB = GetPricingOfferByObjectId(b.Id);
                if (offerA != null && offerB == null) return -1;
                if (offerA == null && offerB != null) return 1;
                return 0;
            });
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}