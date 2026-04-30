using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Models.AvailableTerms;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.Upsell;
using RentoomBookingWeb.Components.Enums;
using RentoomBookingWeb.Helpers;
using RentoomBookingWeb.Services;
using RentoomBooking.SharedFrontend.Components.Shared.UpsellComponents;

namespace RentoomBookingWeb.Components.Features.ReservationWorkflow.Pages
{
    public partial class ApartmentPage : ComponentBase, IDisposable
    {
        [Parameter] public int Id { get; set; }
        [Parameter] public string? Slug { get; set; }
        [Parameter] public Guid? ReservationTokenGuid { get; set; }
        [Parameter] public string? StartDate { get; set; }
        [Parameter] public string? EndDate { get; set; }
        [Parameter] public string? Adults { get; set; }
        [Parameter] public string? Children { get; set; }

        [Inject] public IApartmentsService ApartmentsService { get; set; } = default!;
        [Inject] public IIdoApartmentService IdoApartmentService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public IIdoOfferService OfferService { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] public IAvailabilityFinderService2 AvailabilityFinder { get; set; } = default!;
        [Inject] public NavigationManager NavManager { get; set; } = default!;
        [Inject] public ReservationWorkflowTelemetry WorkflowTelemetry { get; set; } = default!;
        [Inject] internal IStringLocalizer<Apartment> Localizer { get; set; } = default!;
        [Inject] internal IStringLocalizer<Upsell> UpsellLocalizer { get; set; } = default!;
        [Inject] public IReservationWorkflowService ReservationWorkflowService { get; set; } = default!;
        [Inject] public IUpsellCatalogService UpsellCatalogService { get; set; } = default!;
        [Inject] public MediaCacheService MediaCache { get; set; } = default!;
        [Inject] internal IStringLocalizer<Currency> CurrencyLocalizer { get; set; } = default!;
        [Inject] public GoogleAnalyticsService GoogleAnalytics { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;

        protected ApartmentObject? _apartment;
        protected List<ObjectMedium>? _objectMediums = null;
        protected List<ObjectAmenity>? _amenities = null;
        protected int? _bedsCount = null;
        protected bool _isExpanded = false;
        protected PricingOffersResponse? _offersResponse;

        protected bool _isOfferLoading = true;
        protected Guid? _reservationTokenGuid;
        protected ReservationSummaryDto? _reservationDraftSummary;

        protected List<AvailableTerm> _suggestionDates = new();
        protected AvailableTerm? _selectedSuggestionDate = null;
        protected bool _suggestionLoading = false;

        protected List<DefinedAddonEntity>? _definedAddons = null;
        protected List<SelectedAddonDto> _selectedAddons = new();
        protected List<SelectedAddonDto> _mandatoryAddons = new();

        protected IReadOnlyList<UpsellTileDto> _availableUpsells = Array.Empty<UpsellTileDto>();
        protected ReservationPricingContext? _reservationPricingContext;
        protected List<SelectedUpsellDto> _selectedUpsells = new();
        protected StartReservationRequest? _draftStartReservationRequest;

        public UpsellTextConfig UpsellTexts { get; set; } = new();

        protected bool _isModalOpen = false;
        protected bool _isSearchModalOpen = false;

        protected const int SmartScrollOffsetPx = 150;
        protected bool _isAtSummary = false;
        protected bool _isAtOffers = false;
        private IDisposable? _scrollObjRef;

        protected bool _isMobileExpanded = false;

        [JSInvokable]
        public void UpdateScrollState(string elementId, bool isPastOrVisible)
        {
            if (elementId == "confirm-reservation")
            {
                if (_isAtSummary != isPastOrVisible)
                {
                    _isAtSummary = isPastOrVisible;
                    StateHasChanged();
                }
            }
            else if (elementId == "offers-section")
            {
                if (_isAtOffers != isPastOrVisible)
                {
                    _isAtOffers = isPastOrVisible;
                    StateHasChanged();
                }
            }
        }

        protected void ToggleMobileExpand()
        {
            _isMobileExpanded = !_isMobileExpanded;
        }

        protected void OpenSearchModal()
        {
            _isSearchModalOpen = true;
        }

        protected void CloseSearchModal()
        {
            _isSearchModalOpen = false;
        }

        protected decimal TotalAddonsPrice
        {
            get
            {
                decimal total = 0;
                if (_reservationPricingContext is null || _selectedAddons == null || _definedAddons == null) return 0m;

                foreach (var selected in _selectedAddons)
                {
                    var def = _definedAddons.FirstOrDefault(d => d.IdoBookingId == selected.AddonId);
                    var details = def?.AddonDefinition?.Details?.FirstOrDefault(d => d.Lang == "PL");

                    if (details == null) continue;

                    total += AddonPricingCalculator.CalculateTotal(
                                                                    selected.PaymentType,
                                                                    (decimal)selected.Price,
                                                                    _reservationPricingContext.Nights,
                                                                    _reservationPricingContext.TotalGuests,
                                                                    quantity: selected.Quantity);
                }

                return total;
            }
        }

        protected decimal TotalMandatoryAddonsPrice
        {
            get
            {
                decimal total = 0;
                if (_reservationPricingContext is null || _selectedAddons == null || _definedAddons == null) return 0m;

                foreach (var addon in _mandatoryAddons)
                {
                    var def = _definedAddons.FirstOrDefault(d => d.IdoBookingId == addon.AddonId);
                    var details = def?.AddonDefinition?.Details?.FirstOrDefault(d => d.Lang == "PL");

                    if (details == null) continue;

                    total += AddonPricingCalculator.CalculateTotal(
                                                                    addon.PaymentType,
                                                                    (decimal)addon.Price,
                                                                    _reservationPricingContext.Nights,
                                                                    _reservationPricingContext.TotalGuests,
                                                                    quantity: addon.Quantity);
                }

                return total;
            }
        }

        protected decimal TotalUpsellsPrice
        {
            get
            {
                if (_reservationPricingContext is null || _selectedUpsells.Count == 0 || _availableUpsells.Count == 0)
                {
                    return 0m;
                }

                decimal total = 0m;
                foreach (var selected in _selectedUpsells)
                {
                    var tile = _availableUpsells.FirstOrDefault(item => item.PartnerServiceId == selected.PartnerServiceId);
                    if (tile is null)
                    {
                        continue;
                    }

                    var quantity = Math.Max(1, selected.Quantity);
                    total += UpsellPricingCalculator.CalculateTotal(
                                                                    tile.PricingModel,
                                                                    tile.Price,
                                                                    _reservationPricingContext.Nights,
                                                                    _reservationPricingContext.TotalGuests,
                                                                    quantity);
                }

                return total;
            }
        }

        protected const string OfferTypeRefundable = "refundable";
        protected const string OfferTypeNonrefundable = "nonrefundable";
        protected string? _selectedOfferType;

        protected string GetNightsSummaryText()
        {
            if (_reservationPricingContext == null) return string.Empty;
            var nights = _reservationPricingContext.Nights;
            string nightsKey = nights switch
            {
                1 => "StayNight_1",
                var n when n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 10 || n % 100 >= 20) => "StayNight_234",
                _ => "StayNight_Many"
            };
            return $"{nights} {Localizer[nightsKey]}";
        }

        protected string GetPersonsSummaryText()
        {
            if (_reservationPricingContext == null) return string.Empty;
            var guests = _reservationPricingContext.TotalGuests;
            string personsKey = guests switch
            {
                1 => "StayPerson_1",
                var g when g % 10 >= 2 && g % 10 <= 4 && (g % 100 < 10 || g % 100 >= 20) => "StayPerson_234",
                _ => "StayPerson_Many"
            };
            return $"{guests} {Localizer[personsKey]}";
        }

        protected string GetStaySummaryText()
        {
            if (_reservationPricingContext == null) return string.Empty;
            return $"{GetNightsSummaryText()} / {GetPersonsSummaryText()}";
        }

        protected bool HasActiveReservation =>
            _reservationTokenGuid.HasValue &&
            _reservationDraftSummary is not null &&
            _reservationDraftSummary.PaymentStatus != PaymentStatuses.Paid;

        protected string GetOfferButtonLabel(string? offerType)
            => HasActiveReservation && IsSelectedOffer(offerType)
                ? Localizer["ContinueReservation"]
                : Localizer["BookNow"];

        private static Dictionary<string, string> _languageMap = new Dictionary<string, string>
        {
            { "pl-PL", "pol" },
            { "en-US", "eng" }
        };

        private static Dictionary<string, string> _addonslanguageMap = new Dictionary<string, string>
        {
            { "pl-PL", "PL" },
            { "en-US", "EN" },
        };

        protected string CurrentAddonLanguage => _addonslanguageMap?.GetValueOrDefault(
            CultureInfo.CurrentUICulture.Name,
            CultureInfo.CurrentUICulture.Name
        ) ?? CultureInfo.CurrentUICulture.Name;

        protected string CurrentLanguage => _languageMap?.GetValueOrDefault(
            CultureInfo.CurrentUICulture.Name,
            CultureInfo.CurrentUICulture.Name
        ) ?? CultureInfo.CurrentUICulture.Name;

        protected string GetSeoTitle()
        {
            if (_apartment == null) return Localizer["Apartment_SeoTitleFallback"];
            return $"{_apartment.Name} - Rentoom";
        }

        protected string GetSeoDescription()
        {
            if (_objectDescription != null && !string.IsNullOrEmpty(_objectDescription.ShortDescription))
            {
                return Regex.Replace(_objectDescription.ShortDescription, "<.*?>", String.Empty).Trim();
            }

            if (_apartment != null)
            {
                return Localizer["Apartment_SeoDescriptionWithName", _apartment.Name ?? string.Empty, _bedsCount?.ToString() ?? "0"];
            }

            return Localizer["Apartment_SeoDescriptionFallback"];
        }

        protected string GetSeoKeywords()
        {
            return Localizer["Apartment_SeoKeywords", _apartment?.Name ?? string.Empty];
        }

        protected string GetSeoImage()
        {
            var img = _objectMediums?.FirstOrDefault()?.Url;
            if (!string.IsNullOrEmpty(img))
            {
                return img;
            }
            return $"{NavManager.BaseUri}assets/images/header-bg-contact.jpeg";
        }

        protected string GetCanonicalUrl()
        {
            return $"{NavManager.BaseUri}apartamenty/{Id}/{Slug}";
        }

        protected MarkupString GetJsonLd()
        {
            if (_apartment == null) return new MarkupString("");

            var apartmentUnit = new Dictionary<string, object>
            {
                ["@type"] = "Apartment",
                ["name"] = _apartment.Name ?? "",
                ["numberOfRooms"] = _apartment.BedroomsCount ?? 1,
                ["occupancy"] = new Dictionary<string, object>
                {
                    ["@type"] = "QuantitativeValue",
                    ["minValue"] = _apartment.MinCapacity ?? 1,
                    ["maxValue"] = _apartment.Capacity ?? 1,
                    ["value"] = _apartment.Capacity ?? 1
                },
                ["floorSize"] = new Dictionary<string, object>
                {
                    ["@type"] = "QuantitativeValue",
                    ["value"] = string.IsNullOrEmpty(_apartment.Area) ? "0" : _apartment.Area,
                    ["unitCode"] = "MTK"
                }
            };

            if (_amenities != null && _amenities.Any())
            {
                apartmentUnit["amenityFeature"] = _amenities.Select(a => new Dictionary<string, object>
                {
                    ["@type"] = "LocationFeatureSpecification",
                    ["name"] = a.Name,
                    ["value"] = true
                }).ToList();
            }

            var jsonLd = new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@type"] = "VacationRental",
                ["identifier"] = _apartment.Id.ToString(),
                ["name"] = _apartment.Name ?? "",
                ["description"] = GetSeoDescription(),
                ["url"] = GetCanonicalUrl(),
                ["image"] = _objectMediums?.Select(m => m.Url).ToList() ?? new List<string> { GetSeoImage() },

                ["address"] = new Dictionary<string, object>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = _apartment.ObjectLocation?.LocalizationItem?.Street ?? "",
                    ["addressLocality"] = _apartment.ObjectLocation?.LocalizationItem?.City ?? "",
                    ["postalCode"] = _apartment.ObjectLocation?.LocalizationItem?.ZipCode ?? "",
                    ["addressCountry"] = "PL"
                },

                ["geo"] = new Dictionary<string, object>
                {
                    ["@type"] = "GeoCoordinates",
                    ["latitude"] = _apartment.ObjectLocation?.LocalizationItem?.GeoLocationLat?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                    ["longitude"] = _apartment.ObjectLocation?.LocalizationItem?.GeoLocationLng?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""
                },

                ["containsPlace"] = new[] { apartmentUnit }
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = System.Text.Json.JsonSerializer.Serialize(jsonLd, options);
            return new MarkupString(json);
        }

        protected bool IsPolish => CultureInfo.CurrentUICulture.Name.StartsWith("pl", StringComparison.OrdinalIgnoreCase);

        protected string ApartmentBreadcrumbFallbackName => Localizer["Apartment_BreadcrumbFallbackName"];

        protected bool HasRouteDates => TryParseDate(StartDate, out _) && TryParseDate(EndDate, out _);

        protected override async Task OnInitializedAsync()
        {
            _reservationTokenGuid = ReservationTokenGuid;

            _apartment = await ApartmentsService.GetApartmentByIdAsync(Id);
            await GetObjectMedia();

            if (_reservationTokenGuid.HasValue)
            {
                await TryLoadReservationDraftAsync();
            }

            if (_apartment != null)
            {
                WorkflowTelemetry.TrackEvent(
                    "ApartmentDetailViewed",
                    new Dictionary<string, string?>
                    {
                        ["ApartmentId"] = _apartment.Id.ToString(),
                        ["ApartmentName"] = _apartment.Name,
                        ["ReservationTokenGuid"] = _reservationTokenGuid?.ToString(),
                        ["StartDate"] = StartDate,
                        ["EndDate"] = EndDate,
                        ["Adults"] = Adults,
                        ["Children"] = Children
                    });

                await GetObjectDescription(_apartment.Id, CurrentLanguage);
                _amenities = await GetAmenities(_apartment.Id);
                _bedsCount = _apartment?.BedsConfiguration?.Sum(item => item.Count);

                _definedAddons = await ApartmentsService.GetDefinedAddonsAsync();
                InitializeMandatoryAddons();
                UpdateReservationPricingContext();
                RefreshAddonsParams();
                await LoadUpsellsAsync();

            }

            await GetOffer();

            UpsellTexts = new UpsellTextConfig()
            {
                Title = UpsellLocalizer["ListTitle"],
                SubTitle = UpsellLocalizer["ListSubtitle"],
                BadgeInfo = UpsellLocalizer["BadgeInfo"],
                NightsText = UpsellLocalizer["Nights"],
                GuestsText = UpsellLocalizer["Guests"],
                DeleteText = UpsellLocalizer["Delete"],
                AddText = UpsellLocalizer["Add"],
                DescriptionText = UpsellLocalizer["Description"],
                TermsTitleText = UpsellLocalizer["TermsTitle"],
                TermsDescriptionText = UpsellLocalizer["TermsDescription"],
                PricingPerApartmentPerDayText = UpsellLocalizer["PricingPerApartmentPerDay"],
                PricingOneTimeText = UpsellLocalizer["PricingOneTime"],
                PricingPerPersonPerDayText = UpsellLocalizer["PricingPerPersonPerDay"],
                PriceLabel = UpsellLocalizer["Price"],
                Currency = CurrencyLocalizer["PLN"],
            };
        }

        private async Task TryLoadReservationDraftAsync()
        {
            if (!_reservationTokenGuid.HasValue)
            {
                return;
            }

            try
            {
                _reservationDraftSummary = await ReservationWorkflowService.BuildDraftSummaryAsync(_reservationTokenGuid.Value);
            }
            catch
            {
                _reservationTokenGuid = null;
                _reservationDraftSummary = null;
                return;
            }

            var start = _reservationDraftSummary?.StartRequest;
            if (start is null || start.ObjectId != Id)
            {
                _reservationTokenGuid = null;
                _reservationDraftSummary = null;
                return;
            }

            StartDate = start.StartDate.ToString("yyyy-MM-dd");
            EndDate = start.EndDate.ToString("yyyy-MM-dd");
            Adults = start.Adults.ToString();
            Children = start.Children.ToString();

            _selectedOfferType = start.SelectedOfferType;
            _selectedAddons = start.SelectedAddons ?? new List<SelectedAddonDto>();
            _mandatoryAddons = start.MandatoryAddons ?? new List<SelectedAddonDto>();
            _selectedUpsells = start.SelectedUpsells ?? new List<SelectedUpsellDto>();
        }

        private bool _observerInitialized = false;
        private bool _googlePageViewTracked = false;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!_observerInitialized)
            {
                _observerInitialized = true;

                _scrollObjRef = DotNetObjectReference.Create(this);
                await Task.Delay(100);
                await JSRuntime.InvokeVoidAsync("registerScrollObserver", "confirm-reservation", _scrollObjRef);
                await JSRuntime.InvokeVoidAsync("registerScrollObserver", "offers-section", _scrollObjRef);
            }

            if (firstRender && !_googlePageViewTracked && _apartment != null)
            {
                _googlePageViewTracked = true;
                await GoogleAnalytics.TrackEventAsync("apartment_detail_view", new Dictionary<string, object?>
                {
                    ["apartment_id"] = _apartment.Id,
                    ["apartment_name"] = _apartment.Name,
                    ["start_date"] = StartDate,
                    ["end_date"] = EndDate,
                    ["adults"] = Adults,
                    ["children"] = Children,
                    ["has_reservation_token"] = _reservationTokenGuid.HasValue ? 1 : 0
                });
            }
        }

        protected async Task GetOffer()
        {
            _isOfferLoading = true;
            _suggestionDates.Clear();
            _selectedSuggestionDate = null;
            _suggestionLoading = false;

            StateHasChanged();

            try
            {
                _offersResponse = await OfferService.GetPricingOffersAsync(CurrentRequest);
            }
            finally
            {
                _isOfferLoading = false;
            }

            EnsureSelectedOffer();

            bool hasOffers = _offersResponse?.Result?.PricingOffers != null &&
                             _offersResponse.Result.PricingOffers.Any();

            WorkflowTelemetry.TrackEvent(
                hasOffers ? "ReservationOfferLoaded" : "ReservationOfferUnavailable",
                new Dictionary<string, string?>
                {
                    ["ReservationTokenGuid"] = _reservationTokenGuid?.ToString(),
                    ["ApartmentId"] = _apartment?.Id.ToString(),
                    ["StartDate"] = StartDate,
                    ["EndDate"] = EndDate,
                    ["Adults"] = Adults,
                    ["Children"] = Children
                },
                new Dictionary<string, double>
                {
                    ["OfferCount"] = _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers?.Count ?? 0
                });

            if (!hasOffers && !_isOfferLoading)
            {
                _ = Task.Run(async () => await LoadSuggestionInBackground());
            }
        }

        private async Task LoadSuggestionInBackground()
        {
            _suggestionLoading = true;
            await InvokeAsync(StateHasChanged);

            try
            {
                var aptId = _apartment?.Id ?? 0;
                var sDate = StartDate;
                var eDate = EndDate;
                var aCount = int.TryParse(Adults, out var a) ? a : 2;
                var cCount = int.TryParse(Children, out var c) ? c : 0;

                if (aptId == 0) return;

                var result = await AvailabilityFinder.FindAvailableTermsForApartmentAsync(
                    aptId,
                    sDate,
                    eDate,
                    aCount,
                    cCount
                );

                _suggestionDates = (result.AvailableTerms ?? new List<AvailableTerm>())
                    .Take(3)
                    .ToList();
                _selectedSuggestionDate = _suggestionDates.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading suggestions: {ex.Message}");
            }
            finally
            {
                _suggestionLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        protected bool IsSelectedOffer(string? offerType)
        {
            if (string.IsNullOrWhiteSpace(offerType) || string.IsNullOrWhiteSpace(_selectedOfferType))
            {
                return false;
            }

            return string.Equals(_selectedOfferType, offerType, StringComparison.OrdinalIgnoreCase);
        }

        protected void EnsureSelectedOffer()
        {
            var offers = _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers;
            if (offers == null || !offers.Any())
            {
                _selectedOfferType = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedOfferType) &&
                offers.Any(o => string.Equals(o.OfferType, _selectedOfferType, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var nonRefundable = offers.FirstOrDefault(o => string.Equals(o.OfferType, OfferTypeNonrefundable, StringComparison.OrdinalIgnoreCase));
            _selectedOfferType = nonRefundable?.OfferType ?? offers.First().OfferType;
        }

        protected void InitializeMandatoryAddons()
        {
            if (_apartment?.Addons == null || _definedAddons == null) return;

            var mandatoryAddonIds = _apartment.Addons
                .Where(addon => addon.Id.HasValue && addon.Optional == false)
                .Select(addon => addon.Id!.Value)
                .Distinct()
                .ToHashSet();

            _mandatoryAddons.RemoveAll(addon => !mandatoryAddonIds.Contains(addon.AddonId));

            foreach (var addonId in mandatoryAddonIds)
            {
                if (!_mandatoryAddons.Any(x => x.AddonId == addonId))
                {
                    AddAddonToList(_mandatoryAddons, addonId);
                }
            }
        }

        protected void AddAddonToList(List<SelectedAddonDto> targetAddons, int addonId)
        {
            var definition = _definedAddons?.FirstOrDefault(d => d.IdoBookingId == addonId);

            int nights = 1;
            if (TryParseDate(StartDate, out var s) && TryParseDate(EndDate, out var e))
            {
                var diff = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days;
                if (diff > 0) nights = diff;
            }

            int persons = (int.TryParse(Adults, out var a) ? a : 1) + (int.TryParse(Children, out var c) ? c : 0);

            var newDto = new SelectedAddonDto
            {
                AddonId = addonId,
                Quantity = 1,
                Price = definition != null ? (float)definition.PriceGross : 0f,
                Vat = 8f,
                Persons = persons,
                Nights = nights,
                PaymentType = definition?.PaymentType,
                DisplayText = definition?.AddonDefinition?.Details?.FirstOrDefault(d => d.Lang == CurrentAddonLanguage)?.Name ?? "Addon"
            };

            targetAddons.Add(newDto);
        }

        protected bool IsAddonSelected(int? addonId)
        {
            if (!addonId.HasValue) return false;
            return _selectedAddons.Any(a => a.AddonId == addonId.Value);
        }

        protected int GetSelectedAddonQuantity(int? addonId)
        {
            if (!addonId.HasValue) return 1;
            var item = _selectedAddons.FirstOrDefault(x => x.AddonId == addonId.Value);
            return item?.Quantity ?? 1;
        }

        protected void UpdateAddonQuantity(int? addonId, object? value)
        {
            if (!addonId.HasValue || value == null) return;

            if (int.TryParse(value.ToString(), out int quantity))
            {
                var item = _selectedAddons.FirstOrDefault(x => x.AddonId == addonId.Value);

                if (quantity > 0)
                {
                    if (item != null)
                    {
                        item.Quantity = quantity;
                    }
                }
                else
                {
                    if (item != null)
                    {
                        _selectedAddons.Remove(item);
                    }
                }
                StateHasChanged();
            }
        }

        protected bool IsAddonMandatory(bool? isOptional)
        {
            return isOptional == false;
        }

        protected void ToggleAddonWrapper(int? addonId, bool? optional)
        {
            if (IsAddonMandatory(optional)) return;
            ToggleAddon(addonId, !IsAddonSelected(addonId));
        }

        protected void ToggleAddon(int? addonId, bool isChecked)
        {
            if (!addonId.HasValue) return;

            if (isChecked)
            {
                if (!_selectedAddons.Any(x => x.AddonId == addonId.Value))
                {
                    AddAddonToList(_selectedAddons, addonId.Value);
                }
            }
            else
            {
                var item = _selectedAddons.FirstOrDefault(x => x.AddonId == addonId.Value);
                if (item != null)
                {
                    _selectedAddons.Remove(item);
                }
            }
        }

        protected void ToggleExpand()
        {
            _isExpanded = !_isExpanded;
        }

        protected async Task GetObjectMedia()
        {
            if (_apartment != null)
            {
                _objectMediums = await MediaCache.GetOrFetchMediaAsync(
                    _apartment.Id,
                    async () => await IdoApartmentService.GetObjectMediaFromIdoSellAsync(_apartment.Id) ?? new List<ObjectMedium>()
                );
            }
        }

        protected ObjectDescription? _objectDescription = null;
        protected async Task GetObjectDescription(int objectId, string? language)
        {
            var descriptions = await IdoApartmentService.GetObjectDescriptionsAsync(objectId, language);
            _objectDescription = descriptions?.FirstOrDefault();
        }

        protected async Task<List<ObjectAmenity>?> GetAmenities(int objectId)
        {
            return await IdoApartmentService.GetObjectAmenitiesAsync(objectId);
        }

        protected PricingOffersRequest CurrentRequest => new PricingOffersRequest
        {
            ObjectIds = _apartment != null ? new List<int> { _apartment.Id } : null,
            DateFrom = StartDate,
            DateTo = EndDate,
            NumberOfAdults = int.TryParse(Adults, out var a) ? a : null,
            NumberOfBigChildren = int.TryParse(Children, out var c) ? c : null
        };

        protected async Task HandleSearch(Dictionary<string, string> query, bool updateUrl = true)
        {
            _isOfferLoading = true;
            StateHasChanged();

            query.TryGetValue("startDate", out var startDate);
            query.TryGetValue("endDate", out var endDate);
            query.TryGetValue("adults", out var adults);
            query.TryGetValue("children", out var children);

            StartDate = startDate;
            EndDate = endDate;
            Adults = adults;
            Children = children;

            RefreshAddonsParams();
            UpdateReservationPricingContext();

            if (_apartment != null)
            {
                var url = BuildApartmentUrl(_reservationTokenGuid);
                Navigation.NavigateTo(url, forceLoad: false);
                await GetOffer();
            }
        }

        protected async Task GoToSuggestionDates(AvailableTerm? selectedTerm = null)
        {
            var suggestion = selectedTerm ?? _selectedSuggestionDate ?? _suggestionDates.FirstOrDefault();
            if (_apartment != null && suggestion?.StartDate != null && suggestion?.EndDate != null)
            {
                WorkflowTelemetry.TrackEvent(
                    "ReservationSuggestionSelected",
                    new Dictionary<string, string?>
                    {
                        ["ReservationTokenGuid"] = _reservationTokenGuid?.ToString(),
                        ["ApartmentId"] = _apartment.Id.ToString(),
                        ["SuggestedStartDate"] = suggestion.StartDate,
                        ["SuggestedEndDate"] = suggestion.EndDate
                    });

                StartDate = suggestion.StartDate;
                EndDate = suggestion.EndDate;
                RefreshAddonsParams();
                UpdateReservationPricingContext();

                var url = BuildApartmentUrl(_reservationTokenGuid);
                Navigation.NavigateTo(url, forceLoad: false);
                await GetOffer();
            }
        }

        protected void SelectSuggestionDate(AvailableTerm term)
        {
            _selectedSuggestionDate = term;
        }

        protected static string FormatSuggestionDateRange(AvailableTerm term)
        {
            return $"{term.StartDate} - {term.EndDate}";
        }

        protected string BuildApartmentUrl(Guid? reservationGuid)
        {
            var apartmentId = _apartment?.Id ?? Id;
            var slug = _apartment?.Name?.ToSlug() ?? Slug ?? string.Empty;
            var url = $"/apartamenty/{apartmentId}/{slug}";

            if (reservationGuid.HasValue)
            {
                url += $"/{reservationGuid.Value}";
            }

            if (!string.IsNullOrWhiteSpace(StartDate) &&
                !string.IsNullOrWhiteSpace(EndDate) &&
                !string.IsNullOrWhiteSpace(Adults))
            {
                var childrenValue = string.IsNullOrWhiteSpace(Children) ? "0" : Children;
                url += $"/{StartDate}/{EndDate}/{Adults}/{childrenValue}";
            }

            return url;
        }

        protected async Task<Guid> EnsureReservationAsync(StartReservationRequest request)
        {
            if (HasActiveReservation && _reservationTokenGuid.HasValue)
            {
                try
                {
                    await ReservationWorkflowService.UpdateStartRequestAsync(_reservationTokenGuid.Value, request);
                    return _reservationTokenGuid.Value;
                }
                catch
                {
                    _reservationTokenGuid = null;
                }
            }

            var reservationGuid = await ReservationWorkflowService.StartAsync(request);
            _reservationTokenGuid = reservationGuid;
            return reservationGuid;
        }

        protected async Task GoToPayment(string? offerType)
        {
            if (_apartment is null || string.IsNullOrWhiteSpace(StartDate) || string.IsNullOrWhiteSpace(EndDate))
            {
                return;
            }

            if (!TryParseDate(StartDate, out var start) || !TryParseDate(EndDate, out var end))
            {
                return;
            }

            EnsureSelectedOffer();
            var resolvedOfferType = !string.IsNullOrWhiteSpace(offerType) ? offerType : _selectedOfferType;
            var selectedOffer = GetOfferByType(resolvedOfferType);
            var adults = int.TryParse(Adults, out var a) ? a : 1;
            var children = int.TryParse(Children, out var c) ? c : 0;
            var hasEarlyCheckInAddon = _selectedAddons.Exists(a => a.AddonId == 40);
            var hasLateCheckOutAddon = _selectedAddons.Exists(a => a.AddonId == 41);

            var checkInTime = new StartReservationRequest().CheckInTime;
            var checkOutTime = new StartReservationRequest().CheckOutTime;

            if (hasEarlyCheckInAddon)
            {
                checkInTime = checkInTime.AddHours(-1);
            }

            if (hasLateCheckOutAddon)
            {
                checkOutTime = checkOutTime.AddHours(1);
            }

            var request = new StartReservationRequest
            {
                ObjectId = _apartment.Id,
                ObjectItemId = _apartment.Items[0].Id.Value,
                StartDate = start,
                EndDate = end,
                CheckInTime = checkInTime,
                CheckOutTime = checkOutTime,
                Adults = adults,
                Children = children,
                SelectedOfferType = selectedOffer?.OfferType ?? resolvedOfferType,
                OfferPrice = selectedOffer?.Price ?? _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.MinimalPrice,
                Currency = "PLN",
                SelectedAddons = _selectedAddons,
                MandatoryAddons = _mandatoryAddons,
                SelectedUpsells = _selectedUpsells,
                SelectedAddonsTotalPrice = TotalAddonsPrice,
                MandatoryAddonsTotalPrice = TotalMandatoryAddonsPrice,
                SelectedUpsellsTotalPrice = TotalUpsellsPrice
            };

            WorkflowTelemetry.TrackEvent(
                "ReservationStepCompleted_ApartmentSelection",
                new Dictionary<string, string?>
                {
                    ["ReservationTokenGuid"] = _reservationTokenGuid?.ToString(),
                    ["ApartmentId"] = _apartment.Id.ToString(),
                    ["OfferType"] = request.SelectedOfferType,
                    ["StartDate"] = StartDate,
                    ["EndDate"] = EndDate,
                    ["Adults"] = adults.ToString(),
                    ["Children"] = children.ToString()
                },
                new Dictionary<string, double>
                {
                    ["SelectedAddonsCount"] = _selectedAddons.Count,
                    ["SelectedUpsellsCount"] = _selectedUpsells.Count,
                    ["SelectedAddonsTotalPrice"] = (double)TotalAddonsPrice,
                    ["SelectedUpsellsTotalPrice"] = (double)TotalUpsellsPrice
                });

            var reservationGuid = await EnsureReservationAsync(request);
            var apartmentUrl = BuildApartmentUrl(reservationGuid);
            Navigation.NavigateTo(apartmentUrl, replace: true);
            Navigation.NavigateTo($"/rezerwuj/{reservationGuid}/dane-klienta", true);
        }

        protected async Task SmartScrollAction()
        {
            await Task.Yield();

            if (_isAtOffers)
            {
                EnsureSelectedOffer();
                return;
            }

            if (_isAtSummary)
            {
                await JSRuntime.InvokeVoidAsync("scrollToElement", "offers-section", SmartScrollOffsetPx);
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("scrollToElement", "confirm-reservation", SmartScrollOffsetPx);
            }

            if (_isMobileExpanded)
            {
                _isMobileExpanded = false;
                StateHasChanged();
            }
        }

        protected static bool TryParseDate(string? value, out DateOnly date)
        {
            if (!string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out date))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out var parsed))
            {
                date = DateOnly.FromDateTime(parsed);
                return true;
            }

            date = default;
            return false;
        }

        protected OfferItem? GetOfferByType(string? offerType)
        {
            var offers = _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers;
            if (offers is null) return null;

            if (!string.IsNullOrWhiteSpace(offerType))
            {
                var match = offers.FirstOrDefault(o => string.Equals(o.OfferType, offerType, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            var nonRefundable = offers.FirstOrDefault(o => o.OfferType.ToLowerInvariant() == OfferTypeNonrefundable);
            return nonRefundable ?? offers.FirstOrDefault();
        }

        protected void RefreshAddonsParams()
        {
            int currentNights = 1;
            if (TryParseDate(StartDate, out var s) && TryParseDate(EndDate, out var e))
            {
                var diff = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days;
                if (diff > 0) currentNights = diff;
            }

            int currentPersons = (int.TryParse(Adults, out var a) ? a : 1) +
                                 (int.TryParse(Children, out var c) ? c : 0);

            foreach (var addon in _selectedAddons)
            {
                addon.Nights = currentNights;
                addon.Persons = currentPersons;
            }

            foreach (var addon in _mandatoryAddons)
            {
                addon.Nights = currentNights;
                addon.Persons = currentPersons;
            }

            StateHasChanged();
        }

        protected async Task LoadUpsellsAsync()
        {
            if (_apartment is null)
            {
                _availableUpsells = Array.Empty<UpsellTileDto>();
                return;
            }

            try
            {
                _availableUpsells = await UpsellCatalogService.GetUpsellTilesForApartmentAsync(
                    _apartment.Id,
                    CultureInfo.CurrentUICulture.Name,
                    "rentoombooking");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading upsells: {ex.Message}");
                _availableUpsells = Array.Empty<UpsellTileDto>();
            }
        }

        protected void UpdateReservationPricingContext()
        {
            if (!TryParseDate(StartDate, out var start) || !TryParseDate(EndDate, out var end))
            {
                _reservationPricingContext = null;
                return;
            }

            var adults = int.TryParse(Adults, out var parsedAdults) ? parsedAdults : 0;
            var children = int.TryParse(Children, out var parsedChildren) ? parsedChildren : 0;

            _reservationPricingContext = new ReservationPricingContext
            {
                StartDate = start,
                EndDate = end,
                Adults = adults,
                Children = children,
                Currency = "PLN"
            };
        }

        protected void HandleUpsellSelectionChanged(List<SelectedUpsellDto> selectedUpsells)
        {
            _selectedUpsells = selectedUpsells ?? new List<SelectedUpsellDto>();
        }

        protected void OpenModal()
        {
            _isModalOpen = true;
        }

        protected string GetRefundableOfferText()
        {
            if (!TryParseDate(StartDate, out var startDate))
            {
                return Localizer["RefundableOfferTextFallback"];
            }

            var freeCancellationDeadline = startDate.AddDays(-14);
            return Localizer["RefundableOfferText", freeCancellationDeadline.ToString("dd.MM.yyyy", CultureInfo.CurrentUICulture)];
        }

        protected OfferItem? localRefundableOffer =>
            _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers?
                .FirstOrDefault(o => string.Equals(o.OfferType, OfferTypeRefundable, StringComparison.OrdinalIgnoreCase));

        protected OfferItem? localNonRefundableOffer =>
            _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers?
                .FirstOrDefault(o => string.Equals(o.OfferType, OfferTypeNonrefundable, StringComparison.OrdinalIgnoreCase));

        protected decimal? localMinPrice
        {
            get
            {
                var minPrice = (decimal?)localRefundableOffer?.Price;
                if (localNonRefundableOffer != null && (minPrice == null || (decimal?)localNonRefundableOffer.Price < minPrice))
                {
                    minPrice = (decimal?)localNonRefundableOffer.Price;
                }
                return minPrice;
            }
        }

        protected bool localHasOffers => localMinPrice != null;

        public void Dispose()
        {
            _scrollObjRef?.Dispose();
        }
    }
}
