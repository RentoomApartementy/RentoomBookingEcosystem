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
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Partners.Models.Bonuses;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using RentoomBooking.SharedClasses.Services.Descriptions;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Bonuses;
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
        [Inject] public IApartmentAiDescriptionService AiDescriptionService { get; set; } = default!;
        [Inject] public IUpsellCatalogService UpsellCatalogService { get; set; } = default!;
        [Inject] public IBonusesService BonusesService { get; set; } = default!;
        [Inject] public MediaCacheService MediaCache { get; set; } = default!;
        [Inject] public IApartmentMediaCatalogService ApartmentMediaCatalogService { get; set; } = default!;
        [Inject] internal IStringLocalizer<Currency> CurrencyLocalizer { get; set; } = default!;
        [Inject] public GoogleAnalyticsService GoogleAnalytics { get; set; } = default!;
        [Inject] public IWebHostEnvironment Environment { get; set; } = default!;
        [Inject] public RentoomBookingWeb.Services.Localization.IRouteLocalizationService RouteService { get; set; } = default!;

        protected ApartmentObject? _apartment;
        protected ApartmentAiDescriptionDto? _aiDescription;
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
        protected string _bonusInputName = string.Empty;
        protected string _appliedBonusInputName = string.Empty;
        protected string? _bonusStatusMessage;
        protected bool _bonusStatusIsError;
        protected bool _isApplyingBonus;
        protected decimal _bonusDiscountAmount;
        protected decimal _bonusBaseAmount;
        protected string? _bonusAppliedName;
        protected int? _bonusAppliedId;
        protected BonusDiscountValueType? _bonusAppliedValueType;
        protected decimal _bonusAppliedValue;
        protected string? _bonusRejectReason;

        public UpsellTextConfig UpsellTexts { get; set; } = new();

        protected bool _isModalOpen = false;
        protected bool _isSearchModalOpen = false;

        protected const int SmartScrollOffsetPx = 150;
        protected bool _isAtSummary = false;
        protected bool _isAtOffers = false;
        private IDisposable? _scrollObjRef;
        private IJSObjectReference? _scrollModule;

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

        protected decimal TotalPriceAdjustment => TotalAddonsPrice + TotalUpsellsPrice - _bonusDiscountAmount;

        protected Task OnBonusCodeChanged(string value)
        {
            _bonusInputName = value ?? string.Empty;
            _bonusStatusMessage = null;
            _bonusStatusIsError = false;
            return Task.CompletedTask;
        }

        protected async Task ApplyBonusOnApartmentPageAsync()
        {
            if (_isApplyingBonus)
            {
                return;
            }

            var normalized = _bonusInputName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                ClearActiveBonus();
                _bonusStatusIsError = false;
                _bonusStatusMessage = Localizer["BonusRemovedMessage"];
                return;
            }

            _isApplyingBonus = true;
            try
            {
                var result = await EvaluateBonusForCodeAsync(normalized);
                if (result.IsApplied)
                {
                    ApplyActiveBonus(result, normalized);
                    _bonusStatusIsError = false;
                    _bonusStatusMessage = Localizer["BonusAppliedMessage", _bonusDiscountAmount.ToString("N2", CultureInfo.CurrentCulture)] + (_bonusAppliedValueType == BonusDiscountValueType.Percent ?$" ({_bonusAppliedValue} %)":"");
                }
                else
                {
                    _bonusStatusIsError = true;
                    _bonusStatusMessage = GetBonusMessageFromReason(result.RejectReason);
                    await RecalculateActiveBonusPreviewAsync();
                }
            }
            finally
            {
                _isApplyingBonus = false;
            }
        }

        private async Task RecalculateActiveBonusPreviewAsync()
        {
            if (string.IsNullOrWhiteSpace(_appliedBonusInputName))
            {
                ClearActiveBonus();
                return;
            }

            var result = await EvaluateBonusForCodeAsync(_appliedBonusInputName);
            if (result.IsApplied)
            {
                ApplyActiveBonus(result, _appliedBonusInputName);
                return;
            }

            ClearActiveBonus();
            _bonusRejectReason = result.RejectReason;
        }

        private async Task<BonusCalculationResult> EvaluateBonusForCodeAsync(string bonusCode)
        {
            if (_apartment is null
                || string.IsNullOrWhiteSpace(StartDate)
                || string.IsNullOrWhiteSpace(EndDate)
                || !TryParseDate(StartDate, out var reservationStartDate)
                || !TryParseDate(EndDate, out var reservationEndDate))
            {
                return new BonusCalculationResult
                {
                    IsApplied = false,
                    NormalizedBonusInputName = bonusCode,
                    RejectReason = "missing_reservation_data"
                };
            }

            EnsureSelectedOffer();
            var selectedOffer = GetOfferByType(_selectedOfferType);
            var offerPrice = selectedOffer?.Price ?? _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.MinimalPrice;
            if (!offerPrice.HasValue)
            {
                return new BonusCalculationResult
                {
                    IsApplied = false,
                    NormalizedBonusInputName = bonusCode,
                    RejectReason = "missing_offer_price"
                };
            }

            var reservationDays = Math.Max(0, (reservationEndDate.ToDateTime(TimeOnly.MinValue) - reservationStartDate.ToDateTime(TimeOnly.MinValue)).Days);
            var totalCostGrossAmount = offerPrice.Value + TotalAddonsPrice + TotalUpsellsPrice;

            return await BonusesService.EvaluateAsync(new BonusCalculationRequest
            {
                BonusInputName = bonusCode,
                BookingChannel = BookingChannel.WebDirect,
                ReservationStartDate = reservationStartDate,
                ReservationDays = reservationDays,
                ApartmentItemId = _apartment.Items?.FirstOrDefault()?.Id ?? 0,
                OfferPrice = offerPrice.Value,
                MandatoryAddonsTotalPrice = TotalMandatoryAddonsPrice,
                TotalCostGrossAmount = totalCostGrossAmount
            });
        }

        private void ApplyActiveBonus(BonusCalculationResult result, string sourceBonusCode)
        {
            _appliedBonusInputName = sourceBonusCode.Trim();
            _bonusInputName = _appliedBonusInputName;
            _bonusDiscountAmount = result.DiscountAmountPln;
            _bonusBaseAmount = result.BonusBasePln;
            _bonusAppliedName = result.AppliedBonusName;
            _bonusAppliedId = result.AppliedBonusId;
            _bonusAppliedValueType = result.AppliedBonusValueType;
            _bonusAppliedValue = result.AppliedBonusValue;
            _bonusRejectReason = null;
        }

        private void ClearActiveBonus()
        {
            _appliedBonusInputName = string.Empty;
            _bonusDiscountAmount = 0m;
            _bonusBaseAmount = 0m;
            _bonusAppliedName = null;
            _bonusAppliedId = null;
            _bonusAppliedValueType = null;
            _bonusAppliedValue = 0m;
            _bonusRejectReason = null;
        }

        private string GetBonusMessageFromReason(string? reason) => reason switch
        {
            "not_found" => Localizer["BonusReasonNotFound"],
            "disabled" => Localizer["BonusReasonDisabled"],
            "invalid_target" => Localizer["BonusReasonInvalidTarget"],
            "outside_reservation_dates" => Localizer["BonusReasonOutsideReservationDates"],
            "below_minimum_reservation_days" => Localizer["BonusReasonBelowMinimumReservationDays"],
            "below_minimum_order_gross_amount" => Localizer["BonusReasonBelowMinimumOrderGrossAmount"],
            "channel_not_supported" => Localizer["BonusReasonChannelNotSupported"],
            "no_discount" => Localizer["BonusReasonNoDiscount"],
            "missing_reservation_data" => Localizer["BonusReasonMissingReservationData"],
            "missing_offer_price" => Localizer["BonusReasonMissingOfferPrice"],
            _ => Localizer["BonusReasonUnknown"]
        };

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

        protected string CurrentAddonLanguage 
        {
            get
            {
                var culture = CultureInfo.CurrentUICulture.Name;
                if (_addonslanguageMap != null && _addonslanguageMap.TryGetValue(culture, out var mapped))
                {
                    return mapped;
                }
                
                // Extract 2-letter uppercase code (e.g., 'it-IT' -> 'IT')
                return culture.Split('-')[0].ToUpperInvariant();
            }
        }

        protected string CurrentLanguage => _languageMap?.GetValueOrDefault(
            CultureInfo.CurrentUICulture.Name,
            CultureInfo.CurrentUICulture.Name
        ) ?? CultureInfo.CurrentUICulture.Name;

        protected string CurrentAmenityLanguage => CurrentLanguage;

        protected string GetSeoTitle()
        {
            if (_aiDescription != null && !string.IsNullOrEmpty(_aiDescription.MetaTitle)) return _aiDescription.MetaTitle;
            if (_apartment == null) return Localizer["Apartment_SeoTitleFallback"];
            return $"{_apartment.Name} - Rentoom";
        }

        protected string GetSeoDescription()
        {
            if (_aiDescription != null && !string.IsNullOrEmpty(_aiDescription.MetaDescription)) return _aiDescription.MetaDescription;
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
            var localizedBase = RouteService.GetLocalizedUrl("ApartmentDetail");
            return $"{NavManager.BaseUri.TrimEnd('/')}{localizedBase}/{Id}/{Slug}";
        }

        protected MarkupString GetJsonLd()
        {
            var apartment = _apartment;
            if (apartment == null) return new MarkupString("");

            var apartmentUnit = new Dictionary<string, object>
            {
                ["@type"] = "Apartment",
                ["name"] = apartment.Name ?? "",
                ["numberOfRooms"] = apartment.BedroomsCount ?? 1,
                ["occupancy"] = new Dictionary<string, object>
                {
                    ["@type"] = "QuantitativeValue",
                    ["minValue"] = apartment.MinCapacity ?? 1,
                    ["maxValue"] = apartment.Capacity ?? 1,
                    ["value"] = apartment.Capacity ?? 1
                },
                ["floorSize"] = new Dictionary<string, object>
                {
                    ["@type"] = "QuantitativeValue",
                    ["value"] = string.IsNullOrEmpty(apartment.Area) ? "0" : apartment.Area,
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

            var vacationRental = new Dictionary<string, object>
            {
                ["@type"] = "VacationRental",
                ["identifier"] = apartment.Id.ToString(),
                ["name"] = apartment.Name ?? "",
                ["description"] = GetSeoDescription(),
                ["url"] = GetCanonicalUrl(),
                ["image"] = _objectMediums?.Select(m => m.Url ?? string.Empty).ToList() ?? new List<string> { GetSeoImage() },

                ["address"] = new Dictionary<string, object>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = apartment.ObjectLocation?.LocalizationItem?.Street ?? "",
                    ["addressLocality"] = apartment.ObjectLocation?.LocalizationItem?.City ?? "",
                    ["postalCode"] = apartment.ObjectLocation?.LocalizationItem?.ZipCode ?? "",
                    ["addressCountry"] = "PL"
                },

                ["geo"] = new Dictionary<string, object>
                {
                    ["@type"] = "GeoCoordinates",
                    ["latitude"] = apartment.ObjectLocation?.LocalizationItem?.GeoLocationLat?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                    ["longitude"] = apartment.ObjectLocation?.LocalizationItem?.GeoLocationLng?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""
                },

                ["containsPlace"] = new[] { apartmentUnit }
            };

            var graphItems = new List<object> { vacationRental };

            if (_aiDescription?.Faqs != null && _aiDescription.Faqs.Any())
            {
                var faqPage = new Dictionary<string, object>
                {
                    ["@type"] = "FAQPage",
                    ["mainEntity"] = _aiDescription.Faqs.Select(faq => new Dictionary<string, object>
                    {
                        ["@type"] = "Question",
                        ["name"] = faq.Question ?? "",
                        ["acceptedAnswer"] = new Dictionary<string, object>
                        {
                            ["@type"] = "Answer",
                            ["text"] = faq.Answer ?? ""
                        }
                    }).ToList()
                };

                graphItems.Add(faqPage);
            }

            var jsonLd = new Dictionary<string, object>
            {
                ["@context"] = "https://schema.org",
                ["@graph"] = graphItems
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

                try
                {
                    _aiDescription = await AiDescriptionService.GetActiveDescriptionAsync(_apartment.Id, CultureInfo.CurrentUICulture.Name);
                    if (_aiDescription != null)
                    {
                        Console.WriteLine($"[AI-Description] SUCCESS: Found AI description for ApartmentId: {_apartment.Id}, Language: {CultureInfo.CurrentUICulture.Name}, Variant: {_aiDescription.VariantType}");
                    }
                    else
                    {
                        Console.WriteLine($"[AI-Description] INFO: No AI description found for ApartmentId: {_apartment.Id}, Language: {CultureInfo.CurrentUICulture.Name}. Using IdoBooking fallback.");
                       
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI-Description] ERROR: Exception while fetching AI description: {ex.Message}");
                }

                _amenities = await GetAmenities(_apartment.Id);
                _bedsCount = _apartment?.BedsConfiguration?.Sum(item => item.Count);

                _definedAddons = await ApartmentsService.GetDefinedAddonsAsync();
                InitializeMandatoryAddons();
                UpdateReservationPricingContext();
                RefreshAddonsParams();
                await LoadUpsellsAsync();

            }

            await GetOffer();
            await RecalculateActiveBonusPreviewAsync();

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
            _bonusInputName = start.BonusInputName ?? string.Empty;
            _appliedBonusInputName = _bonusInputName;
            _bonusDiscountAmount = start.DiscountAmountPln;
            _bonusBaseAmount = start.BonusBasePln;
            _bonusAppliedName = start.AppliedBonusName;
            _bonusAppliedId = start.AppliedBonusId;
            _bonusAppliedValueType = start.AppliedBonusValueType;
            _bonusAppliedValue = start.AppliedBonusValue;
            _bonusRejectReason = start.BonusRejectReason;
        }

        private bool _observerInitialized = false;
        private bool _googlePageViewTracked = false;
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!_observerInitialized)
            {
                _observerInitialized = true;
                _scrollModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/scrollObserver.js");

                _scrollObjRef = DotNetObjectReference.Create(this);
                await Task.Delay(100);
                await _scrollModule.InvokeVoidAsync("registerScrollObserver", "confirm-reservation", _scrollObjRef);
                await _scrollModule.InvokeVoidAsync("registerScrollObserver", "offers-section", _scrollObjRef);
            }

            if (firstRender && !_googlePageViewTracked && _apartment != null)
            {
                _googlePageViewTracked = true;
                var reservationNights = GetReservationNights();
                var selectedOffer = GetOfferByType(_selectedOfferType);
                var offerTotalPrice = selectedOffer?.Price ?? _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.MinimalPrice ?? 0m;
                var reservationValue = Math.Max(0m, offerTotalPrice + TotalPriceAdjustment);
                var apartmentItem = BuildGa4ApartmentItem(_apartment, offerTotalPrice, reservationNights);

                await GoogleAnalytics.TrackEventAsync("view_item", new Dictionary<string, object?>
                {
                    ["currency"] = "PLN",
                    ["value"] = reservationValue,
                    ["items"] = new[] { apartmentItem },
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
            await RecalculateActiveBonusPreviewAsync();

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

            var nonRefundable = offers.FirstOrDefault(o => o != null && string.Equals(o.OfferType, OfferTypeNonrefundable, StringComparison.OrdinalIgnoreCase));
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

            var mandatoryAddonsForApartment = _apartment.Addons
                .Where(addon => addon.Id.HasValue && addon.Optional == false)
                .Distinct()
                .ToList();

            _mandatoryAddons.RemoveAll(addon => !mandatoryAddonIds.Contains(addon.AddonId));

            /*   foreach (var addonId in mandatoryAddonIds)
               {
                   if (!_mandatoryAddons.Any(x => x.AddonId == addonId))
                   {
                       AddAddonToList(_mandatoryAddons, addonId);
                   }
               }*/

            foreach (var addon in mandatoryAddonsForApartment)
            {
                if (!_mandatoryAddons.Any(x => x.AddonId == addon.Id))
                {
                    AddAddonToList(_mandatoryAddons, addon);
                }

            }
        }

        protected void AddAddonToList(List<SelectedAddonDto> targetAddons, AddonType addon)
        {
            var definition = _definedAddons?.FirstOrDefault(d => d.IdoBookingId == addon.Id);

            int nights = 1;
            if (TryParseDate(StartDate, out var s) && TryParseDate(EndDate, out var e))
            {
                var diff = (e.ToDateTime(TimeOnly.MinValue) - s.ToDateTime(TimeOnly.MinValue)).Days;
                if (diff > 0) nights = diff;
            }

            int persons = (int.TryParse(Adults, out var a) ? a : 1) + (int.TryParse(Children, out var c) ? c : 0);

            var details = definition?.AddonDefinition?.Details;
            var newDto = new SelectedAddonDto
            {
                AddonId = addon.Id!.Value,
                Quantity = 1,
                Price = definition != null ? (float)definition.PriceGross : 0f,
                Vat = 8f,
                Persons = persons,
                Nights = nights,
                PaymentType = definition?.PaymentType,
                DisplayText = definition?.AddonDefinition?.Details?.FirstOrDefault(d => d.Lang == CurrentAddonLanguage)?.Name ?? addon.Name ?? $"Addon {addon.Id}"
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
                _ = RecalculateActiveBonusPreviewAsync();
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

            var addon = _apartment?.Addons?.FirstOrDefault(a => a.Id == addonId.Value);

            if (isChecked)
            {
                if (!_selectedAddons.Any(x => x.AddonId == addonId.Value))
                {
                    AddAddonToList(_selectedAddons, addon!);
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

            _ = RecalculateActiveBonusPreviewAsync();
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
                    async () => await ApartmentMediaCatalogService.GetApartmentMediaAsync(_apartment.Id)
                );
            }
        }

        protected ObjectDescription? _objectDescription = null;
        protected async Task GetObjectDescription(int objectId, string? language)
        {
            Console.WriteLine("Object description from ido");
            var descriptions = await IdoApartmentService.GetObjectDescriptionsAsync(objectId, language);
            _objectDescription = descriptions?.FirstOrDefault();
        }

        protected async Task<List<ObjectAmenity>?> GetAmenities(int objectId)
        {
            var amenities = await ApartmentsService.GetApartmentAmenitiesAsync(CurrentAmenityLanguage, objectId);

            return amenities
                .Select(x => new ObjectAmenity
                {
                    Id = x.AmenityId,
                    ObjectId = objectId,
                    Name = x.AmenityName,
                    IconName= x.IconSource
                })
                .ToList();
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
            var localizedBase = RouteService.GetLocalizedUrl("ApartmentDetail");
            var url = $"{localizedBase}/{apartmentId}/{slug}";

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
            var apartment = _apartment;
            if (apartment is null || string.IsNullOrWhiteSpace(StartDate) || string.IsNullOrWhiteSpace(EndDate))
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
                ObjectId = apartment.Id,
                ObjectItemId = apartment.Items.FirstOrDefault()?.Id ?? 0,
                StartDate = start,
                EndDate = end,
                CheckInTime = checkInTime,
                CheckOutTime = checkOutTime,
                Adults = adults,
                Children = children,
                SelectedOfferType = selectedOffer?.OfferType ?? resolvedOfferType ?? string.Empty,
                OfferPrice = selectedOffer?.Price ?? _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.MinimalPrice,
                Currency = "PLN",
                BookingChannel = BookingChannel.WebDirect,
                SelectedAddons = _selectedAddons,
                MandatoryAddons = _mandatoryAddons,
                SelectedUpsells = _selectedUpsells,
                SelectedAddonsTotalPrice = TotalAddonsPrice,
                MandatoryAddonsTotalPrice = TotalMandatoryAddonsPrice,
                SelectedUpsellsTotalPrice = TotalUpsellsPrice,
                BonusInputName = _appliedBonusInputName,
                AppliedBonusId = _bonusAppliedId,
                AppliedBonusName = _bonusAppliedName,
                AppliedBonusValueType = _bonusAppliedValueType,
                AppliedBonusValue = _bonusAppliedValue,
                BonusBasePln = _bonusBaseAmount,
                DiscountAmountPln = _bonusDiscountAmount,
                BonusRejectReason = _bonusRejectReason,
                Vat =8
            };

            WorkflowTelemetry.TrackEvent(
                "ReservationStepCompleted_ApartmentSelection",
                new Dictionary<string, string?>
                {
                    ["ReservationTokenGuid"] = _reservationTokenGuid?.ToString(),
                    ["ApartmentId"] = apartment.Id.ToString(),
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

            var reservationNights = GetReservationNights(start, end);
            var reservationValue = Math.Max(0m, (request.OfferPrice ?? 0m) + request.SelectedAddonsTotalPrice + request.SelectedUpsellsTotalPrice - request.DiscountAmountPln);
            var apartmentItem = BuildGa4ApartmentItem(apartment, request.OfferPrice ?? 0m, reservationNights);

            await GoogleAnalytics.TrackEventAsync("begin_checkout", new Dictionary<string, object?>
            {
                ["currency"] = request.Currency,
                ["value"] = reservationValue,
                ["items"] = new[] { apartmentItem },
                ["start_date"] = request.StartDate.ToString("yyyy-MM-dd"),
                ["end_date"] = request.EndDate.ToString("yyyy-MM-dd"),
                ["has_reservation_token"] = _reservationTokenGuid.HasValue ? 1 : 0
            });

            var reservationGuid = await EnsureReservationAsync(request);
            var apartmentUrl = BuildApartmentUrl(reservationGuid);
            Navigation.NavigateTo(apartmentUrl, replace: true);
            Navigation.NavigateTo($"/rezerwuj/{reservationGuid}/dane-klienta", true);
        }

        protected async Task SmartScrollAction()
        {
            await Task.Yield();
            _scrollModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/scrollObserver.js");

            if (_isAtOffers)
            {
                EnsureSelectedOffer();
                return;
            }

            if (_isAtSummary)
            {
                await _scrollModule.InvokeVoidAsync("scrollToElement", "offers-section", SmartScrollOffsetPx);
            }
            else
            {
                await _scrollModule.InvokeVoidAsync("scrollToElement", "confirm-reservation", SmartScrollOffsetPx);
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

        private int GetReservationNights()
        {
            if (!TryParseDate(StartDate, out var start) || !TryParseDate(EndDate, out var end))
            {
                return 1;
            }

            return GetReservationNights(start, end);
        }

        private static int GetReservationNights(DateOnly start, DateOnly end)
        {
            return Math.Max(1, end.DayNumber - start.DayNumber);
        }

        private static decimal GetUnitPricePerNight(decimal totalOfferPrice, int nights)
        {
            if (nights <= 0)
            {
                return totalOfferPrice;
            }

            return Math.Round(totalOfferPrice / nights, 2, MidpointRounding.AwayFromZero);
        }

        private static Dictionary<string, object?> BuildGa4ApartmentItem(ApartmentObject apartment, decimal totalOfferPrice, int nights)
        {
            return new Dictionary<string, object?>
            {
                ["item_id"] = apartment.Id.ToString(),
                ["item_name"] = apartment.Name,
                ["item_category"] = apartment.ObjectLocation?.LocalizationItem?.City,
                ["price"] = GetUnitPricePerNight(totalOfferPrice, nights),
                ["quantity"] = nights
            };
        }

        protected OfferItem? GetOfferByType(string? offerType)
        {
            var offers = _offersResponse?.Result?.PricingOffers?.FirstOrDefault()?.Offers;
            if (offers is null) return null;

            if (!string.IsNullOrWhiteSpace(offerType))
            {
                var match = offers.FirstOrDefault(o => o != null && string.Equals(o.OfferType, offerType, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            var nonRefundable = offers.FirstOrDefault(o => o != null && string.Equals(o.OfferType, OfferTypeNonrefundable, StringComparison.OrdinalIgnoreCase));
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
