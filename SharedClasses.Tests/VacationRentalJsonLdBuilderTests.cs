using System.Text.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.Seo;
using Xunit;

namespace SharedClasses.Tests;

public class VacationRentalJsonLdBuilderTests
{
    [Fact]
    public void BasePage_EmitsNightlyPriceNearestTermAndInStock()
    {
        var input = CreateInput() with
        {
            FromOffer = new VacationRentalFromOfferInput
            {
                PricePerNight = 425.50m,
                Currency = "PLN",
                AvailabilityStarts = new DateOnly(2026, 9, 10),
                AvailabilityEnds = new DateOnly(2026, 9, 13),
                Nights = 3,
                Adults = 2,
                Children = 1,
                Name = "Od 425,50 PLN / noc",
                Url = "https://rentoom.pl/apartament/17/test/2026-09-10/2026-09-13/2/1"
            }
        };

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var rental = GetRental(document);
        var offer = rental.GetProperty("makesOffer")[0];

        Assert.Equal("https://schema.org/InStock", offer.GetProperty("availability").GetString());
        Assert.Equal("2026-09-10", offer.GetProperty("availabilityStarts").GetString());
        Assert.Equal("2026-09-13", offer.GetProperty("availabilityEnds").GetString());
        Assert.Equal(425.50m, offer.GetProperty("priceSpecification").GetProperty("price").GetDecimal());
        Assert.Equal("PLN", offer.GetProperty("priceSpecification").GetProperty("priceCurrency").GetString());
        Assert.Equal("DAY", offer.GetProperty("priceSpecification").GetProperty("unitCode").GetString());
        Assert.Equal(3, offer.GetProperty("eligibleDuration").GetProperty("value").GetInt32());
        Assert.Equal("https://rentoom.pl/apartament/17/test#accommodation", offer.GetProperty("itemOffered").GetProperty("@id").GetString());
    }

    [Fact]
    public void DatedPage_EmitsEveryPositiveRateWithTotalPriceDatesAndGuests()
    {
        var input = CreateInput() with
        {
            DatedOffer = new VacationRentalDatedOfferInput
            {
                LoadState = DatedOfferLoadState.Succeeded,
                AvailabilityStarts = new DateOnly(2026, 10, 1),
                AvailabilityEnds = new DateOnly(2026, 10, 5),
                Adults = 3,
                Children = 2,
                Currency = "PLN",
                Url = "https://rentoom.pl/apartament/17/test/2026-10-01/2026-10-05/3/2",
                Rates = new[]
                {
                    new VacationRentalRateInput("Oferta zwrotna", "Oferta zwrotna; 3 Dorośli, 2 Dzieci", "refundable", 2400m),
                    new VacationRentalRateInput("Oferta bezzwrotna", "Oferta bezzwrotna; 3 Dorośli, 2 Dzieci", "nonrefundable", 2100m)
                }
            }
        };

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var offers = GetRental(document).GetProperty("makesOffer");

        Assert.Equal(2, offers.GetArrayLength());
        Assert.Equal(new[] { 2400m, 2100m }, offers.EnumerateArray().Select(offer => offer.GetProperty("price").GetDecimal()).ToArray());
        Assert.All(offers.EnumerateArray(), offer =>
        {
            Assert.Equal("PLN", offer.GetProperty("priceCurrency").GetString());
            Assert.Equal("2026-10-01", offer.GetProperty("availabilityStarts").GetString());
            Assert.Equal("2026-10-05", offer.GetProperty("availabilityEnds").GetString());
            Assert.Equal("https://schema.org/InStock", offer.GetProperty("availability").GetString());
            Assert.Contains("3 Dorośli, 2 Dzieci", offer.GetProperty("description").GetString());
        });
    }

    [Fact]
    public void SuccessfulRequestWithoutBookableRates_EmitsPricelessSoldOut()
    {
        var input = CreateInput() with
        {
            DatedOffer = new VacationRentalDatedOfferInput
            {
                LoadState = DatedOfferLoadState.Succeeded,
                AvailabilityStarts = new DateOnly(2026, 11, 1),
                AvailabilityEnds = new DateOnly(2026, 11, 3),
                Adults = 2,
                Children = 0,
                SoldOutName = "Brak oferty",
                Rates = new[] { new VacationRentalRateInput("Błędna taryfa", null, "refundable", 0m) }
            }
        };

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var offer = GetRental(document).GetProperty("makesOffer")[0];

        Assert.Equal("https://schema.org/SoldOut", offer.GetProperty("availability").GetString());
        Assert.False(offer.TryGetProperty("price", out _));
        Assert.False(offer.TryGetProperty("priceCurrency", out _));
    }

    [Fact]
    public void FailedDatedRequest_DoesNotFallBackToInStockOrSoldOutOffer()
    {
        var input = CreateInput() with
        {
            FromOffer = new VacationRentalFromOfferInput
            {
                PricePerNight = 300m,
                AvailabilityStarts = new DateOnly(2026, 8, 10),
                AvailabilityEnds = new DateOnly(2026, 8, 12),
                Nights = 2
            },
            DatedOffer = new VacationRentalDatedOfferInput { LoadState = DatedOfferLoadState.Failed }
        };

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));

        Assert.False(GetRental(document).TryGetProperty("makesOffer", out _));
    }

    [Fact]
    public void InvalidAndEmptyPropertyData_IsOmitted_WhileValidNumbersStayNumeric()
    {
        var input = CreateInput();
        input.Apartment.Area = "48,75";

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var rental = GetRental(document);
        var accommodation = rental.GetProperty("containsPlace");

        Assert.Equal(JsonValueKind.Number, rental.GetProperty("geo").GetProperty("latitude").ValueKind);
        Assert.Equal(JsonValueKind.Number, rental.GetProperty("geo").GetProperty("longitude").ValueKind);
        Assert.Equal(JsonValueKind.Number, accommodation.GetProperty("floorSize").GetProperty("value").ValueKind);
        Assert.Equal(48.75m, accommodation.GetProperty("floorSize").GetProperty("value").GetDecimal());
        Assert.Equal(2, accommodation.GetProperty("numberOfBedrooms").GetInt32());
        Assert.False(accommodation.TryGetProperty("numberOfRooms", out _));
        Assert.Equal(new[] { "wifi", "balcony", "Unmapped amenity" },
            accommodation.GetProperty("amenityFeature").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray());

        input.Apartment.Area = "0";
        input.Apartment.ObjectLocation = new ObjectLocation
        {
            LocalizationItem = new LocalizationItem { GeoLocationLat = 0, GeoLocationLng = 0 }
        };
        input = input with
        {
            Images = new[]
            {
                new VacationRentalImageInput(""),
                new VacationRentalImageInput("relative.jpg")
            }
        };

        using var invalidDocument = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var invalidRental = GetRental(invalidDocument);
        Assert.False(invalidRental.TryGetProperty("image", out _));
        Assert.False(invalidRental.TryGetProperty("address", out _));
        Assert.False(invalidRental.TryGetProperty("geo", out _));
        Assert.False(invalidRental.GetProperty("containsPlace").TryGetProperty("floorSize", out _));
    }

    [Theory]
    [InlineData(null, null, null, null, null, false)]
    [InlineData(null, "2026-09-01", null, null, null, true)]
    [InlineData(null, null, null, "2", null, true)]
    [InlineData("21a1cc7d-e711-4dac-86d3-b74506ed9098", null, null, null, null, true)]
    public void RoutePolicy_NoIndexesDatesPartialParametersAndTokens(
        string? token,
        string? start,
        string? end,
        string? adults,
        string? children,
        bool expected)
    {
        var guid = Guid.TryParse(token, out var parsed) ? parsed : (Guid?)null;
        Assert.Equal(expected, ApartmentSeoRoutePolicy.ShouldNoIndex(guid, start, end, adults, children));
    }

    private static VacationRentalJsonLdInput CreateInput()
    {
        var apartment = new ApartmentObject
        {
            Id = 17,
            Name = "Test",
            Capacity = 5,
            MinCapacity = 1,
            BedroomsCount = 2,
            Area = "48.75",
            BedsConfiguration = new List<BedConfigurationArray>
            {
                new() { Count = 1, BedType = "doubleBed" },
                new() { Count = 2, BedType = "singleBed" }
            },
            ObjectLocation = new ObjectLocation
            {
                LocalizationItem = new LocalizationItem
                {
                    Street = "Długa 1",
                    City = "Gdańsk",
                    ZipCode = "80-001",
                    Region = "Pomorskie",
                    Country = "Polska",
                    GeoLocationLat = 54.352f,
                    GeoLocationLng = 18.6466f,
                    CheckInHours = new CheckInHoursRange { From = "15:00" },
                    CheckOutHours = new CheckOutHoursRange { To = "11:00" }
                }
            }
        };

        return new VacationRentalJsonLdInput
        {
            Apartment = apartment,
            CanonicalUrl = "https://rentoom.pl/apartament/17/test",
            Description = "Opis apartamentu",
            Images = new[]
            {
                new VacationRentalImageInput("https://cdn.example.test/a.jpg"),
                new VacationRentalImageInput(""),
                new VacationRentalImageInput("/relative.jpg")
            },
            EnglishAmenities = new[]
            {
                new VacationRentalAmenityInput(1, "Wi-Fi"),
                new VacationRentalAmenityInput(2, "Balcony"),
                new VacationRentalAmenityInput(3, "Unmapped amenity")
            }
        };
    }

    [Fact]
    public void ImagesWithCaption_EmitImageObjectsAndKeepPlainUrlsWithout()
    {
        var input = CreateInput() with
        {
            Images = new[]
            {
                new VacationRentalImageInput("https://cdn.example.test/salon.jpg", "Jasny salon z aneksem kuchennym"),
                new VacationRentalImageInput("https://cdn.example.test/sypialnia.jpg", "   "),
                new VacationRentalImageInput("https://cdn.example.test/salon.jpg", "Duplikat tego samego zdjęcia")
            }
        };

        using var document = JsonDocument.Parse(VacationRentalJsonLdBuilder.Build(input));
        var images = GetRental(document).GetProperty("image").EnumerateArray().ToArray();

        Assert.Equal(2, images.Length);

        Assert.Equal(JsonValueKind.Object, images[0].ValueKind);
        Assert.Equal("ImageObject", images[0].GetProperty("@type").GetString());
        Assert.Equal("https://cdn.example.test/salon.jpg", images[0].GetProperty("url").GetString());
        Assert.Equal("https://cdn.example.test/salon.jpg", images[0].GetProperty("contentUrl").GetString());
        Assert.Equal("Jasny salon z aneksem kuchennym", images[0].GetProperty("caption").GetString());
        Assert.Equal("Jasny salon z aneksem kuchennym", images[0].GetProperty("name").GetString());

        // Whitespace-only captions are treated as missing, so the image stays a bare URL.
        Assert.Equal(JsonValueKind.String, images[1].ValueKind);
        Assert.Equal("https://cdn.example.test/sypialnia.jpg", images[1].GetString());
    }

    private static JsonElement GetRental(JsonDocument document)
        => document.RootElement.GetProperty("@graph").EnumerateArray()
            .Single(item => item.GetProperty("@type").GetString() == "VacationRental");
}
