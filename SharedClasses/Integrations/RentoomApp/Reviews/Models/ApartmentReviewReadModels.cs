using System.Collections.ObjectModel;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;

internal sealed class ApartmentReviewReadEntity
{
    public int Id { get; set; }
    public int ApartmentItemId { get; set; }
    public int Source { get; set; }
    public DateTimeOffset ReviewedAtUtc { get; set; }
    public DateOnly? CheckinDate { get; set; }
    public double? Score { get; set; }
    public int RatingScale { get; set; } = 10;
    public string? PositiveText { get; set; }
    public string? OriginalLang { get; set; }
    public string? GuestName { get; set; }
    public string? GuestCountryName { get; set; }
    public string? GuestType { get; set; }
    public bool IsVisible { get; set; }

    public ApartmentItemReviewReadEntity ApartmentItem { get; set; } = null!;
}

internal sealed class ApartmentItemReviewReadEntity
{
    public int Id { get; set; }
    public int ApartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public RentoomAppApartmentReadEntity Apartment { get; set; } = null!;
}

internal sealed class RentoomAppApartmentReadEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Collection<ApartmentItemReviewReadEntity> ApartmentItems { get; set; } = new();

}

public sealed class ApartmentReviewCardDto
{
    public int Id { get; init; }
    public int ApartmentItemId { get; init; }
    public string ApartmentName { get; init; } = string.Empty;
    public DateTimeOffset ReviewedAtUtc { get; init; }
    public DateOnly? CheckinDate { get; init; }
    public double Score { get; init; }
    public int RatingScale { get; init; } = 10;
    public string PositiveText { get; init; } = string.Empty;
    public string? GuestName { get; init; }
    public string? GuestCountryName { get; init; }
    public string? GuestType { get; init; }
}

public sealed class ApartmentReviewAggregateDto
{
    public double? AverageScore { get; init; }
    public int RatingScale { get; init; } = 10;
    public int ScoredCount { get; init; }
    public bool HasMixedRatingScales { get; init; }
}
