using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Services.Seo;

public enum DatedOfferLoadState
{
    NotRequested,
    Succeeded,
    Failed
}

public sealed record VacationRentalAmenityInput(int Id, string? EnglishName);

public sealed record VacationRentalRateInput(
    string? Name,
    string? Description,
    string? OfferType,
    decimal Price);

public sealed class VacationRentalFromOfferInput
{
    public decimal PricePerNight { get; init; }
    public string Currency { get; init; } = "PLN";
    public DateOnly AvailabilityStarts { get; init; }
    public DateOnly AvailabilityEnds { get; init; }
    public int Nights { get; init; }
    public int Adults { get; init; }
    public int Children { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
}

public sealed class VacationRentalDatedOfferInput
{
    public DatedOfferLoadState LoadState { get; init; }
    public DateOnly AvailabilityStarts { get; init; }
    public DateOnly AvailabilityEnds { get; init; }
    public int Adults { get; init; }
    public int Children { get; init; }
    public string Currency { get; init; } = "PLN";
    public string? Url { get; init; }
    public string? SoldOutName { get; init; }
    public string? SoldOutDescription { get; init; }
    public IReadOnlyList<VacationRentalRateInput> Rates { get; init; } = Array.Empty<VacationRentalRateInput>();
}

/// <param name="Url">Absolute image URL. Relative or non-http values are dropped.</param>
/// <param name="Caption">Alt text in the page culture. When present the image is emitted as an ImageObject.</param>
public sealed record VacationRentalImageInput(string? Url, string? Caption = null);

public sealed record VacationRentalJsonLdInput
{
    public required ApartmentObject Apartment { get; init; }
    public required string CanonicalUrl { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<VacationRentalImageInput> Images { get; init; } = Array.Empty<VacationRentalImageInput>();
    public IReadOnlyList<VacationRentalAmenityInput> EnglishAmenities { get; init; } = Array.Empty<VacationRentalAmenityInput>();
    public VacationRentalFromOfferInput? FromOffer { get; init; }
    public VacationRentalDatedOfferInput? DatedOffer { get; init; }
}

/// <summary>
/// Builds the canonical Schema.org graph for an apartment page. The builder is deliberately
/// independent of Blazor so pricing and availability rules can be covered by unit tests.
/// </summary>
public static class VacationRentalJsonLdBuilder
{
    private const string Schema = "https://schema.org/";
    private const string LeaseOut = "http://purl.org/goodrelations/v1#LeaseOut";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default
    };

    private static readonly Dictionary<string, string> AmenityTokens = new(StringComparer.Ordinal)
    {
        ["airconditioning"] = "ac",
        ["airconditioner"] = "ac",
        ["ac"] = "ac",
        ["airportshuttle"] = "airportShuttle",
        ["balcony"] = "balcony",
        ["beachaccess"] = "beachAccess",
        ["privatebeach"] = "privateBeachAccess",
        ["privatebeachaccess"] = "privateBeachAccess",
        ["childfriendly"] = "childFriendly",
        ["childrenwelcome"] = "childFriendly",
        ["crib"] = "crib",
        ["cot"] = "crib",
        ["babycot"] = "crib",
        ["elevator"] = "elevator",
        ["lift"] = "elevator",
        ["fireplace"] = "fireplace",
        ["freebreakfast"] = "freeBreakfast",
        ["gym"] = "gymFitnessEquipment",
        ["fitness"] = "gymFitnessEquipment",
        ["fitnesscenter"] = "gymFitnessEquipment",
        ["fitnessequipment"] = "gymFitnessEquipment",
        ["heating"] = "heating",
        ["hottub"] = "hotTub",
        ["jacuzzi"] = "hotTub",
        ["ironingboard"] = "ironingBoard",
        ["kitchen"] = "kitchen",
        ["kitchenette"] = "kitchen",
        ["microwave"] = "microwave",
        ["microwaveoven"] = "microwave",
        ["outdoorgrill"] = "outdoorGrill",
        ["barbecue"] = "outdoorGrill",
        ["bbq"] = "outdoorGrill",
        ["oven"] = "ovenStove",
        ["stove"] = "ovenStove",
        ["hob"] = "ovenStove",
        ["ovenstove"] = "ovenStove",
        ["patio"] = "patio",
        ["petsallowed"] = "petsAllowed",
        ["petfriendly"] = "petsAllowed",
        ["pool"] = "pool",
        ["swimmingpool"] = "pool",
        ["selfcheckin"] = "selfCheckinCheckout",
        ["selfcheckout"] = "selfCheckinCheckout",
        ["selfcheckincheckout"] = "selfCheckinCheckout",
        ["smokingallowed"] = "smokingAllowed",
        ["tv"] = "tv",
        ["television"] = "tv",
        ["cabletelevision"] = "tv",
        ["flatscreentv"] = "tv",
        ["wheelchairaccessible"] = "wheelchairAccessible",
        ["wheelchairaccess"] = "wheelchairAccessible",
        ["wifi"] = "wifi",
        ["wirelessinternet"] = "wifi",
        ["internetaccess"] = "wifi"
    };

    public static string Build(VacationRentalJsonLdInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Apartment);

        if (!TryGetHttpUri(input.CanonicalUrl, out var canonicalUri))
        {
            throw new ArgumentException("CanonicalUrl must be an absolute HTTP(S) URL.", nameof(input));
        }

        var canonicalUrl = canonicalUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        var siteRoot = canonicalUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var rentalId = $"{canonicalUrl}#vacation-rental";
        var accommodationId = $"{canonicalUrl}#accommodation";
        var organizationId = $"{siteRoot}/#organization";

        var organization = new Dictionary<string, object?>
        {
            ["@type"] = "Organization",
            ["@id"] = organizationId,
            ["name"] = "Rentoom",
            ["url"] = $"{siteRoot}/"
        };

        var accommodation = BuildAccommodation(input.Apartment, accommodationId, input.EnglishAmenities);
        var rental = new Dictionary<string, object?>
        {
            ["@type"] = "VacationRental",
            ["@id"] = rentalId,
            ["identifier"] = input.Apartment.Id.ToString(CultureInfo.InvariantCulture),
            ["additionalType"] = "Apartment",
            ["name"] = input.Apartment.Name?.Trim() ?? string.Empty,
            ["description"] = NullIfWhiteSpace(input.Description),
            ["url"] = canonicalUrl,
            ["brand"] = new Dictionary<string, object?> { ["@id"] = organizationId },
            ["knowsLanguage"] = new[] { "pl-PL", "en-US" },
            ["containsPlace"] = accommodation
        };

        var images = input.Images
            .Where(static image => TryGetHttpUri(image.Url, out _))
            .Select(static image => (Url: image.Url!.Trim(), Caption: NullIfWhiteSpace(image.Caption)))
            .DistinctBy(static image => image.Url, StringComparer.OrdinalIgnoreCase)
            .Select(static image => image.Caption is null
                ? (object)image.Url
                : new Dictionary<string, object?>
                {
                    ["@type"] = "ImageObject",
                    ["url"] = image.Url,
                    ["contentUrl"] = image.Url,
                    ["name"] = image.Caption,
                    ["caption"] = image.Caption
                })
            .ToList();
        AddIfNotEmpty(rental, "image", images);

        AddLocation(rental, input.Apartment);
        AddCheckTimes(rental, input.Apartment);

        var offers = BuildOffers(input, canonicalUrl, accommodationId);
        AddIfNotEmpty(rental, "makesOffer", offers);

        RemoveNullAndEmptyValues(rental);
        RemoveNullAndEmptyValues(accommodation);

        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[] { organization, rental }
        };

        return JsonSerializer.Serialize(graph, SerializerOptions);
    }

    private static Dictionary<string, object?> BuildAccommodation(
        ApartmentObject apartment,
        string accommodationId,
        IReadOnlyList<VacationRentalAmenityInput> amenities)
    {
        var capacity = apartment.Capacity.GetValueOrDefault();
        var accommodation = new Dictionary<string, object?>
        {
            ["@type"] = new[] { "Apartment", "Product" },
            ["@id"] = accommodationId,
            ["additionalType"] = "EntirePlace",
            ["name"] = NullIfWhiteSpace(apartment.Name),
            ["numberOfBedrooms"] = apartment.BedroomsCount is > 0 ? apartment.BedroomsCount : null
        };

        if (capacity > 0)
        {
            var occupancy = new Dictionary<string, object?>
            {
                ["@type"] = "QuantitativeValue",
                ["value"] = capacity,
                ["maxValue"] = capacity,
                ["unitCode"] = "C62"
            };
            if (apartment.MinCapacity is > 0 && apartment.MinCapacity <= capacity)
            {
                occupancy["minValue"] = apartment.MinCapacity;
            }
            accommodation["occupancy"] = occupancy;
        }

        if (TryParsePositiveDecimal(apartment.Area, out var area))
        {
            accommodation["floorSize"] = new Dictionary<string, object?>
            {
                ["@type"] = "QuantitativeValue",
                ["value"] = area,
                ["unitCode"] = "MTK"
            };
        }

        var beds = (apartment.BedsConfiguration ?? new List<BedConfigurationArray>())
            .Where(static bed => bed.Count > 0 && !string.IsNullOrWhiteSpace(bed.BedType))
            .Select(static bed => (object)new Dictionary<string, object?>
            {
                ["@type"] = "BedDetails",
                ["numberOfBeds"] = bed.Count,
                ["typeOfBed"] = NormalizeBedType(bed.BedType!)
            })
            .ToList();
        AddIfNotEmpty(accommodation, "bed", beds);

        var amenityFeatures = amenities
            .Select(static amenity => NormalizeAmenityName(amenity.EnglishName))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static name => (object)new Dictionary<string, object?>
            {
                ["@type"] = "LocationFeatureSpecification",
                ["name"] = name,
                ["value"] = true
            })
            .ToList();
        AddIfNotEmpty(accommodation, "amenityFeature", amenityFeatures);

        return accommodation;
    }

    private static void AddLocation(Dictionary<string, object?> rental, ApartmentObject apartment)
    {
        var location = apartment.ObjectLocation?.LocalizationItem;
        if (location is null)
        {
            return;
        }

        var hasAddressData = !string.IsNullOrWhiteSpace(location.Street) ||
                             !string.IsNullOrWhiteSpace(location.City) ||
                             !string.IsNullOrWhiteSpace(location.Region) ||
                             !string.IsNullOrWhiteSpace(location.ZipCode) ||
                             !string.IsNullOrWhiteSpace(location.Country);
        if (hasAddressData)
        {
            var address = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = NullIfWhiteSpace(location.Street),
                ["addressLocality"] = NullIfWhiteSpace(location.City),
                ["addressRegion"] = NullIfWhiteSpace(location.Region),
                ["postalCode"] = NullIfWhiteSpace(location.ZipCode),
                ["addressCountry"] = NormalizeCountry(location.Country)
            };
            RemoveNullAndEmptyValues(address);
            rental["address"] = address;
        }

        if (location.GeoLocationLat.HasValue && location.GeoLocationLng.HasValue &&
            location.GeoLocationLat.Value != 0 && location.GeoLocationLng.Value != 0 &&
            location.GeoLocationLat.Value is >= -90 and <= 90 &&
            location.GeoLocationLng.Value is >= -180 and <= 180)
        {
            rental["geo"] = new Dictionary<string, object?>
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = Convert.ToDouble(location.GeoLocationLat.Value, CultureInfo.InvariantCulture),
                ["longitude"] = Convert.ToDouble(location.GeoLocationLng.Value, CultureInfo.InvariantCulture)
            };
        }
    }

    private static void AddCheckTimes(Dictionary<string, object?> rental, ApartmentObject apartment)
    {
        var location = apartment.ObjectLocation?.LocalizationItem;
        if (TryNormalizeTime(location?.CheckInHours?.From, out var checkin))
        {
            rental["checkinTime"] = checkin;
        }
        if (TryNormalizeTime(location?.CheckOutHours?.To, out var checkout))
        {
            rental["checkoutTime"] = checkout;
        }
    }

    private static List<object> BuildOffers(VacationRentalJsonLdInput input, string canonicalUrl, string accommodationId)
    {
        if (input.DatedOffer is { } dated)
        {
            if (dated.LoadState != DatedOfferLoadState.Succeeded ||
                !IsValidStay(dated.AvailabilityStarts, dated.AvailabilityEnds))
            {
                return new List<object>();
            }

            var rates = dated.Rates.Where(static rate => rate.Price > 0).ToList();
            if (rates.Count == 0)
            {
                return new List<object>
                {
                    BuildDatedOffer(dated, null, 0, canonicalUrl, accommodationId, soldOut: true)
                };
            }

            return rates
                .Select((rate, index) => (object)BuildDatedOffer(dated, rate, index, canonicalUrl, accommodationId, soldOut: false))
                .ToList();
        }

        if (input.FromOffer is { } from &&
            from.PricePerNight > 0 &&
            from.Nights > 0 &&
            IsValidStay(from.AvailabilityStarts, from.AvailabilityEnds))
        {
            var url = NormalizeOfferUrl(from.Url, canonicalUrl);
            return new List<object>
            {
                new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["@id"] = $"{canonicalUrl}#offer-from-{from.AvailabilityStarts:yyyyMMdd}-{from.AvailabilityEnds:yyyyMMdd}-{from.Adults}-{from.Children}",
                    ["name"] = NullIfWhiteSpace(from.Name),
                    ["description"] = NullIfWhiteSpace(from.Description),
                    ["url"] = url,
                    ["availability"] = $"{Schema}InStock",
                    ["availabilityStarts"] = from.AvailabilityStarts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["availabilityEnds"] = from.AvailabilityEnds.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ["businessFunction"] = LeaseOut,
                    ["itemOffered"] = new Dictionary<string, object?> { ["@id"] = accommodationId },
                    ["priceSpecification"] = new Dictionary<string, object?>
                    {
                        ["@type"] = "UnitPriceSpecification",
                        ["price"] = from.PricePerNight,
                        ["priceCurrency"] = NormalizeCurrency(from.Currency),
                        ["unitCode"] = "DAY",
                        ["unitText"] = "night"
                    },
                    ["eligibleDuration"] = BuildDuration(from.Nights)
                }
            };
        }

        return new List<object>();
    }

    private static Dictionary<string, object?> BuildDatedOffer(
        VacationRentalDatedOfferInput dated,
        VacationRentalRateInput? rate,
        int index,
        string canonicalUrl,
        string accommodationId,
        bool soldOut)
    {
        var nights = dated.AvailabilityEnds.DayNumber - dated.AvailabilityStarts.DayNumber;
        var rateKey = soldOut ? "sold-out" : Slugify(rate?.OfferType ?? rate?.Name ?? $"rate-{index + 1}");
        var offer = new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["@id"] = $"{canonicalUrl}#offer-{dated.AvailabilityStarts:yyyyMMdd}-{dated.AvailabilityEnds:yyyyMMdd}-{dated.Adults}-{dated.Children}-{rateKey}-{index + 1}",
            ["name"] = NullIfWhiteSpace(soldOut ? dated.SoldOutName : rate?.Name),
            ["description"] = NullIfWhiteSpace(soldOut ? dated.SoldOutDescription : rate?.Description),
            ["url"] = NormalizeOfferUrl(dated.Url, canonicalUrl),
            ["availability"] = $"{Schema}{(soldOut ? "SoldOut" : "InStock")}",
            ["availabilityStarts"] = dated.AvailabilityStarts.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["availabilityEnds"] = dated.AvailabilityEnds.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["businessFunction"] = LeaseOut,
            ["itemOffered"] = new Dictionary<string, object?> { ["@id"] = accommodationId },
            ["eligibleDuration"] = BuildDuration(nights)
        };

        if (!soldOut && rate is not null)
        {
            offer["price"] = rate.Price;
            offer["priceCurrency"] = NormalizeCurrency(dated.Currency);
        }

        RemoveNullAndEmptyValues(offer);
        return offer;
    }

    private static Dictionary<string, object?> BuildDuration(int nights) => new()
    {
        ["@type"] = "QuantitativeValue",
        ["value"] = nights,
        ["unitCode"] = "DAY"
    };

    private static bool IsValidStay(DateOnly start, DateOnly end) => end > start;

    private static string NormalizeOfferUrl(string? url, string fallback)
        => TryGetHttpUri(url, out var uri) ? uri.AbsoluteUri : fallback;

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "PLN" : currency.Trim().ToUpperInvariant();

    private static string NormalizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
        {
            return "PL";
        }

        var normalized = NormalizeLookupKey(country);
        if (normalized is "pl" or "pol" or "poland" or "polska")
        {
            return "PL";
        }

        var trimmed = country.Trim();
        return trimmed.Length == 2 ? trimmed.ToUpperInvariant() : "PL";
    }

    private static string NormalizeAmenityName(string? englishName)
    {
        var name = englishName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var key = NormalizeLookupKey(name);
        return AmenityTokens.TryGetValue(key, out var token) ? token : name;
    }

    private static string NormalizeBedType(string value)
    {
        var key = NormalizeLookupKey(value);
        return key switch
        {
            "single" or "singlebed" or "twin" or "twinbed" => "Single",
            "double" or "doublebed" => "Double",
            "full" or "fullbed" => "Full",
            "semidouble" or "semidoublebed" => "SemiDouble",
            "queen" or "queenbed" => "Queen",
            "king" or "kingbed" => "King",
            "californiaking" or "californiakingbed" => "CaliforniaKing",
            _ => value.Trim()
        };
    }

    private static string NormalizeLookupKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }
        return builder.ToString();
    }

    private static string Slugify(string value)
    {
        var key = NormalizeLookupKey(value);
        return string.IsNullOrWhiteSpace(key) ? "rate" : key;
    }

    private static bool TryParsePositiveDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out result) && result > 0;
    }

    private static bool TryNormalizeTime(string? value, out string normalized)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var time))
        {
            normalized = time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static bool TryGetHttpUri(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var parsed) &&
            (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddIfNotEmpty(Dictionary<string, object?> target, string key, List<object> values)
    {
        if (values.Count > 0)
        {
            target[key] = values;
        }
    }

    private static void RemoveNullAndEmptyValues(Dictionary<string, object?> dictionary)
    {
        foreach (var key in dictionary
                     .Where(static pair => pair.Value is null || pair.Value is string text && string.IsNullOrWhiteSpace(text))
                     .Select(static pair => pair.Key)
                     .ToList())
        {
            dictionary.Remove(key);
        }
    }
}

public static class ApartmentSeoRoutePolicy
{
    public static bool ShouldNoIndex(
        Guid? reservationToken,
        string? startDate,
        string? endDate,
        string? adults,
        string? children)
        => reservationToken.HasValue ||
           !string.IsNullOrWhiteSpace(startDate) ||
           !string.IsNullOrWhiteSpace(endDate) ||
           !string.IsNullOrWhiteSpace(adults) ||
           !string.IsNullOrWhiteSpace(children);
}
