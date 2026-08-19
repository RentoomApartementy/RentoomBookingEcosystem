using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews;

public interface IApartmentReviewReader
{
    Task<IReadOnlyList<ApartmentReviewCardDto>> GetLatestReviewsPerApartmentAsync(
        string languageCode,
        double minimumScore = 8.9,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApartmentReviewCardDto>> GetApartmentReviewsAsync(
        int apartmentItemId,
        string languageCode,
        double minimumScore = 8.9,
        int limit = 12,
        CancellationToken cancellationToken = default);
}

public sealed class ApartmentReviewReader : IApartmentReviewReader
{
    internal const int BookingSource = 1;

    private readonly IDbContextFactory<RappReviewsDbContext> _dbContextFactory;

    public ApartmentReviewReader(IDbContextFactory<RappReviewsDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<ApartmentReviewCardDto>> GetLatestReviewsPerApartmentAsync(
        string languageCode,
        double minimumScore = 8.9,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildLatestReviewsPerApartmentQuery(dbContext, languageCode, minimumScore)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApartmentReviewCardDto>> GetApartmentReviewsAsync(
        int apartmentItemId,
        string languageCode,
        double minimumScore = 8.9,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        if (apartmentItemId <= 0 || limit <= 0)
        {
            return Array.Empty<ApartmentReviewCardDto>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await BuildApartmentReviewsQuery(dbContext, apartmentItemId, languageCode, minimumScore, limit)
            .ToListAsync(cancellationToken);
    }

    internal static IQueryable<ApartmentReviewCardDto> BuildLatestReviewsPerApartmentQuery(
        RappReviewsDbContext dbContext,
        string languageCode,
        double minimumScore)
    {
        var reviews = BuildQualifyingQuery(dbContext, languageCode, minimumScore);
        var latestReviewIds = reviews
            .GroupBy(review => review.ApartmentItemId)
            .Select(group => group
                .OrderByDescending(review => review.ReviewedAtUtc)
                .ThenByDescending(review => review.Id)
                .Select(review => review.Id)
                .First());

        return reviews
            .Where(review => latestReviewIds.Contains(review.Id))
            .OrderByDescending(review => review.ReviewedAtUtc)
            .ThenByDescending(review => review.Id)
            .Select(ToCardDto());
    }

    internal static IQueryable<ApartmentReviewCardDto> BuildApartmentReviewsQuery(
        RappReviewsDbContext dbContext,
        int apartmentItemId,
        string languageCode,
        double minimumScore,
        int limit)
        => BuildQualifyingQuery(dbContext, languageCode, minimumScore)
            .Where(review => review.ApartmentItemId == apartmentItemId)
            .OrderByDescending(review => review.ReviewedAtUtc)
            .ThenByDescending(review => review.Id)
            .Take(limit)
            .Select(ToCardDto());

    internal static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageCode).TwoLetterISOLanguageName.ToLowerInvariant();
        }
        catch (CultureNotFoundException)
        {
            var normalized = languageCode.Trim().Replace('_', '-');
            var separatorIndex = normalized.IndexOf('-');
            return (separatorIndex > 0 ? normalized[..separatorIndex] : normalized).ToLowerInvariant();
        }
    }

    private static IQueryable<ApartmentReviewReadEntity> BuildQualifyingQuery(
        RappReviewsDbContext dbContext,
        string languageCode,
        double minimumScore)
    {
        var normalizedLanguage = NormalizeLanguageCode(languageCode);
        if (normalizedLanguage.Length == 0)
        {
            return dbContext.ApartmentReviews.Where(_ => false);
        }

        var languageWithDash = normalizedLanguage + "-";
        var languageWithUnderscore = normalizedLanguage + "_";

        return dbContext.ApartmentReviews
            .AsNoTracking()
            .Where(review =>
                review.Source == BookingSource
                && review.IsVisible
                && !review.ApartmentItem.IsArchived
                && review.Score.HasValue
                && review.Score.Value >= minimumScore
                && review.PositiveText != null
                && review.PositiveText.Trim() != string.Empty
                && review.OriginalLang != null
                && (review.OriginalLang.ToLower() == normalizedLanguage
                    || review.OriginalLang.ToLower().StartsWith(languageWithDash)
                    || review.OriginalLang.ToLower().StartsWith(languageWithUnderscore)));
    }

    private static System.Linq.Expressions.Expression<Func<ApartmentReviewReadEntity, ApartmentReviewCardDto>> ToCardDto()
        => review => new ApartmentReviewCardDto
        {
            Id = review.Id,
            ApartmentItemId = review.ApartmentItemId,
            ApartmentName = review.ApartmentItem.Apartment.Name,
            ReviewedAtUtc = review.ReviewedAtUtc,
            CheckinDate = review.CheckinDate,
            Score = review.Score!.Value,
            RatingScale = review.RatingScale,
            PositiveText = review.PositiveText!.Trim(),
            GuestName = review.GuestName,
            GuestCountryName = review.GuestCountryName,
            GuestType = review.GuestType
        };
}
