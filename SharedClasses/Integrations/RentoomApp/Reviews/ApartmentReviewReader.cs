using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews;

public interface IApartmentReviewReader
{
    Task<IReadOnlyList<ApartmentReviewCardDto>> GetLatestReviewsPerApartmentAsync(
        string languageCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApartmentReviewCardDto>> GetApartmentReviewsAsync(
        int apartmentItemId,
        string languageCode,
        int limit = 12,
        CancellationToken cancellationToken = default);

    Task<ApartmentReviewAggregateDto> GetApartmentReviewAggregateAsync(
        int apartmentItemId,
        CancellationToken cancellationToken = default);
}

public sealed class ApartmentReviewReader : IApartmentReviewReader
{
    internal const int BookingSource = 1;

    private readonly IDbContextFactory<RappReviewsDbContext> _dbContextFactory;
    private readonly double _minimumScore;

    public ApartmentReviewReader(
        IDbContextFactory<RappReviewsDbContext> dbContextFactory,
        IOptions<ApartmentReviewsOptions> options)
    {
        _dbContextFactory = dbContextFactory;
        _minimumScore = options.Value.MinimumScore
            ?? throw new InvalidOperationException(
                $"{ApartmentReviewsOptions.SectionName}:MinimumScore is required.");
    }

    public async Task<IReadOnlyList<ApartmentReviewCardDto>> GetLatestReviewsPerApartmentAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildLatestReviewsPerApartmentQuery(dbContext, languageCode, _minimumScore)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApartmentReviewCardDto>> GetApartmentReviewsAsync(
        int apartmentItemId,
        string languageCode,
        int limit = 12,
        CancellationToken cancellationToken = default)
    {
        if (apartmentItemId <= 0 || limit <= 0)
        {
            return Array.Empty<ApartmentReviewCardDto>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await BuildApartmentReviewsQuery(dbContext, apartmentItemId, languageCode, _minimumScore, limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<ApartmentReviewAggregateDto> GetApartmentReviewAggregateAsync(
        int apartmentItemId,
        CancellationToken cancellationToken = default)
    {
        if (apartmentItemId <= 0)
        {
            return EmptyAggregate();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var scaleStats = await BuildAggregateScaleStatsQuery(dbContext, apartmentItemId)
            .ToListAsync(cancellationToken);

        return CalculateAggregate(scaleStats);
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

    internal static IQueryable<ApartmentReviewScaleStat> BuildAggregateScaleStatsQuery(
        RappReviewsDbContext dbContext,
        int apartmentItemId)
        => dbContext.ApartmentReviews
            .AsNoTracking()
            .Where(review => review.ApartmentItemId == apartmentItemId && review.Score.HasValue)
            .GroupBy(review => review.RatingScale)
            .Select(group => new ApartmentReviewScaleStat(
                group.Key,
                group.Count(),
                group.Sum(review => review.Score!.Value)));

    internal static ApartmentReviewAggregateDto CalculateAggregate(
        IReadOnlyCollection<ApartmentReviewScaleStat> scaleStats)
    {
        var scoredCount = scaleStats.Sum(stat => stat.Count);
        if (scoredCount == 0)
        {
            return EmptyAggregate();
        }

        if (scaleStats.Count == 1)
        {
            var only = scaleStats.First();
            return new ApartmentReviewAggregateDto
            {
                AverageScore = Math.Round(only.Sum / only.Count, 2),
                RatingScale = only.Scale,
                ScoredCount = scoredCount
            };
        }

        const int normalizedScale = 10;
        var normalizedSum = scaleStats.Sum(stat =>
            stat.Scale <= 0 ? 0 : stat.Sum / stat.Scale * normalizedScale);

        return new ApartmentReviewAggregateDto
        {
            AverageScore = Math.Round(normalizedSum / scoredCount, 2),
            RatingScale = normalizedScale,
            ScoredCount = scoredCount,
            HasMixedRatingScales = true
        };
    }

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

    private static ApartmentReviewAggregateDto EmptyAggregate()
        => new();
}

internal sealed record ApartmentReviewScaleStat(int Scale, int Count, double Sum);
