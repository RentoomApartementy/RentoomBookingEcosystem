using System.Globalization;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews;

public static class ReviewPresentationFormatter
{
    public static string FormatAggregateScore(ApartmentReviewAggregateDto aggregate, CultureInfo culture)
        => aggregate.AverageScore?.ToString("0.0", culture) ?? string.Empty;

    public static string FormatScore(ApartmentReviewCardDto review, CultureInfo culture)
        => $"{review.Score.ToString("0.#", culture)}/{review.RatingScale.ToString(culture)}";

    public static string GetAuthorName(ApartmentReviewCardDto review)
        => string.IsNullOrWhiteSpace(review.GuestName) ? "Booking.com" : review.GuestName.Trim();

    public static string GetAuthorInitial(ApartmentReviewCardDto review, CultureInfo culture)
    {
        var authorName = GetAuthorName(review);
        return StringInfo.GetNextTextElement(authorName).ToUpper(culture);
    }

    public static string BuildMetadata(ApartmentReviewCardDto review, CultureInfo culture)
    {
        var parts = new List<string>(3);
        AddIfPresent(parts, review.GuestCountryName);
        AddIfPresent(parts, review.GuestType);

        var date = review.CheckinDate?.ToDateTime(TimeOnly.MinValue)
            ?? review.ReviewedAtUtc.UtcDateTime;
        parts.Add(date.ToString("MMMM yyyy", culture));

        return string.Join(" · ", parts);
    }

    private static void AddIfPresent(ICollection<string> parts, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(value.Trim());
        }
    }
}
