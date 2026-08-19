using System.Globalization;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;
using Xunit;

namespace SharedClasses.Tests;

public sealed class ReviewPresentationFormatterTests
{
    [Fact]
    public void FormatsScoreAndCompleteMetadataUsingCurrentCulture()
    {
        var culture = CultureInfo.GetCultureInfo("pl-PL");
        var review = new ApartmentReviewCardDto
        {
            Score = 9.4,
            RatingScale = 10,
            CheckinDate = new DateOnly(2025, 12, 10),
            ReviewedAtUtc = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero),
            GuestName = " Katarzyna ",
            GuestCountryName = "Polska",
            GuestType = "Rodzina"
        };

        Assert.Equal("9,4/10", ReviewPresentationFormatter.FormatScore(review, culture));
        Assert.Equal("Katarzyna", ReviewPresentationFormatter.GetAuthorName(review));
        Assert.Equal("K", ReviewPresentationFormatter.GetAuthorInitial(review, culture));
        Assert.Equal("Polska · Rodzina · grudzień 2025", ReviewPresentationFormatter.BuildMetadata(review, culture));
    }

    [Fact]
    public void UsesBookingAuthorAndReviewDateWithoutEmptyMetadataSeparators()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var review = new ApartmentReviewCardDto
        {
            Score = 10,
            RatingScale = 10,
            ReviewedAtUtc = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
            GuestName = " ",
            GuestCountryName = null,
            GuestType = "Couple"
        };

        Assert.Equal("Booking.com", ReviewPresentationFormatter.GetAuthorName(review));
        Assert.Equal("B", ReviewPresentationFormatter.GetAuthorInitial(review, culture));
        Assert.Equal("Couple · March 2026", ReviewPresentationFormatter.BuildMetadata(review, culture));
    }
}
