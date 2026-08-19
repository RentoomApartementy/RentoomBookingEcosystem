using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.Reviews.Models;
using Xunit;

namespace SharedClasses.Tests;

public sealed class ApartmentReviewReaderTests
{
    [Fact]
    public async Task LatestPerApartment_AppliesPublicFiltersAndUsesNewestIdAsTieBreaker()
    {
        var factory = CreateFactory();
        await SeedAsync(factory,
            [
                Item(1, "Apartament A"),
                Item(2, "Apartament B"),
                Item(3, "Apartament archiwalny", isArchived: true)
            ],
            [
                Review(1, 1, Date(2026, 1, 1)),
                Review(2, 1, Date(2026, 2, 1)),
                Review(3, 1, Date(2026, 2, 1)),
                Review(4, 1, Date(2026, 4, 1), isVisible: false),
                Review(5, 1, Date(2026, 5, 1), source: 2),
                Review(6, 1, Date(2026, 6, 1), score: 8.8),
                Review(7, 1, Date(2026, 7, 1), positiveText: "   "),
                Review(8, 1, Date(2026, 8, 1), language: "en"),
                Review(9, 2, Date(2026, 3, 1), language: "pl-PL"),
                Review(10, 3, Date(2026, 9, 1))
            ]);

        var reader = CreateReader(factory);
        var result = await reader.GetLatestReviewsPerApartmentAsync("pl-PL");

        Assert.Equal([9, 3], result.Select(review => review.Id));
        Assert.Equal("Apartament B", result[0].ApartmentName);
        Assert.Equal("Apartament A", result[1].ApartmentName);
    }

    [Fact]
    public async Task ApartmentReviews_ReturnsAtMostTwelveNewestQualifyingReviews()
    {
        var factory = CreateFactory();
        var reviews = Enumerable.Range(1, 14)
            .Select(id => Review(id, 1, Date(2026, 1, id)))
            .Append(Review(100, 1, Date(2026, 2, 1), language: "de"))
            .ToArray();

        await SeedAsync(factory, [Item(1, "Apartament A")], reviews);

        var reader = CreateReader(factory);
        var result = await reader.GetApartmentReviewsAsync(1, "pl", limit: 12);

        Assert.Equal(12, result.Count);
        Assert.Equal(Enumerable.Range(3, 12).Reverse(), result.Select(review => review.Id));
    }

    [Fact]
    public async Task Aggregate_UsesAllScoredReviewsIncludingHiddenAndNonBookingReviews()
    {
        var factory = CreateFactory();
        await SeedAsync(factory,
            [Item(1, "Apartament A")],
            [
                Review(1, 1, Date(2026, 1, 1), score: 10),
                Review(2, 1, Date(2026, 1, 2), isVisible: false, score: 8),
                Review(3, 1, Date(2026, 1, 3), source: 2, score: 9),
                Review(4, 1, Date(2026, 1, 4), score: null)
            ]);

        var reader = CreateReader(factory);
        var result = await reader.GetApartmentReviewAggregateAsync(1);

        Assert.Equal(9, result.AverageScore);
        Assert.Equal(10, result.RatingScale);
        Assert.Equal(3, result.ScoredCount);
        Assert.False(result.HasMixedRatingScales);
    }

    [Fact]
    public async Task Aggregate_NormalizesMixedRatingScalesToTen()
    {
        var factory = CreateFactory();
        var booking = Review(1, 1, Date(2026, 1, 1), score: 8);
        var fivePoint = Review(2, 1, Date(2026, 1, 2), source: 2, score: 4);
        fivePoint.RatingScale = 5;
        await SeedAsync(factory, [Item(1, "Apartament A")], [booking, fivePoint]);

        var reader = CreateReader(factory);
        var result = await reader.GetApartmentReviewAggregateAsync(1);

        Assert.Equal(8, result.AverageScore);
        Assert.Equal(10, result.RatingScale);
        Assert.Equal(2, result.ScoredCount);
        Assert.True(result.HasMixedRatingScales);
    }

    [Fact]
    public void Aggregate_RoundsStoredAverageToTwoDecimalPlacesLikeRentoomApp()
    {
        var result = ApartmentReviewReader.CalculateAggregate(
            [new ApartmentReviewScaleStat(10, 3, 25)]);

        Assert.Equal(8.33, result.AverageScore);
    }

    [Fact]
    public async Task Aggregate_ReturnsEmptyResultWhenApartmentHasNoScores()
    {
        var factory = CreateFactory();
        await SeedAsync(factory,
            [Item(1, "Apartament A")],
            [Review(1, 1, Date(2026, 1, 1), score: null)]);

        var reader = CreateReader(factory);
        var result = await reader.GetApartmentReviewAggregateAsync(1);

        Assert.Null(result.AverageScore);
        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(10, result.RatingScale);
    }

    [Theory]
    [InlineData("PL-pl", "pl")]
    [InlineData("en_US", "en")]
    [InlineData(" de ", "de")]
    [InlineData("", "")]
    public void NormalizeLanguageCode_ReturnsTwoLetterLowercaseCode(string input, string expected)
    {
        Assert.Equal(expected, ApartmentReviewReader.NormalizeLanguageCode(input));
    }

    [Fact]
    public async Task PublicReviews_UseConfiguredMinimumScore()
    {
        var factory = CreateFactory();
        await SeedAsync(factory,
            [Item(1, "Apartament A")],
            [
                Review(1, 1, Date(2026, 1, 1), score: 9.3),
                Review(2, 1, Date(2026, 1, 2), score: 9.4)
            ]);

        var reader = CreateReader(factory, minimumScore: 9.4);
        var result = await reader.GetLatestReviewsPerApartmentAsync("pl");

        Assert.Equal([2], result.Select(review => review.Id));
    }

    [Fact]
    public void PublicQueries_AreTranslatedByNpgsqlWithoutClientEvaluation()
    {
        var options = new DbContextOptionsBuilder<RappReviewsDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test;Password=test")
            .Options;
        using var dbContext = new RappReviewsDbContext(options);

        var homeSql = ApartmentReviewReader
            .BuildLatestReviewsPerApartmentQuery(dbContext, "pl-PL", 8.9)
            .ToQueryString();
        var apartmentSql = ApartmentReviewReader
            .BuildApartmentReviewsQuery(dbContext, 42, "pl-PL", 8.9, 12)
            .ToQueryString();
        var aggregateSql = ApartmentReviewReader
            .BuildAggregateScaleStatsQuery(dbContext, 42)
            .ToQueryString();

        Assert.Contains("apartment_reviews", homeSql);
        Assert.Contains("ApartmentItems", homeSql);
        Assert.Contains("LIMIT", apartmentSql);
        Assert.Contains("GROUP BY", aggregateSql);
    }

    private static TestReviewsDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<RappReviewsDbContext>()
            .UseInMemoryDatabase($"reviews-{Guid.NewGuid():N}")
            .Options;
        return new TestReviewsDbContextFactory(options);
    }

    private static ApartmentReviewReader CreateReader(
        IDbContextFactory<RappReviewsDbContext> factory,
        double minimumScore = 8.9)
        => new(factory, Options.Create(new ApartmentReviewsOptions
        {
            MinimumScore = minimumScore
        }));

    private static async Task SeedAsync(
        IDbContextFactory<RappReviewsDbContext> factory,
        IReadOnlyCollection<ApartmentItemReviewReadEntity> items,
        IReadOnlyCollection<ApartmentReviewReadEntity> reviews)
    {
        await using var dbContext = await factory.CreateDbContextAsync();
        dbContext.AddRange(items);
        dbContext.AddRange(reviews);
        await dbContext.SaveChangesAsync();
    }

    private static ApartmentItemReviewReadEntity Item(int id, string name, bool isArchived = false)
        => new()
        {
            Id = id,
            ApartmentId = id,
            Name = name,
            IsArchived = isArchived,
            Apartment = new RentoomAppApartmentReadEntity { Id = id, Name = name }
        };

    private static ApartmentReviewReadEntity Review(
        int id,
        int apartmentItemId,
        DateTimeOffset reviewedAt,
        bool isVisible = true,
        int source = ApartmentReviewReader.BookingSource,
        double? score = 9.4,
        string? positiveText = "Bardzo dobry pobyt.",
        string language = "pl")
        => new()
        {
            Id = id,
            ApartmentItemId = apartmentItemId,
            Source = source,
            ReviewedAtUtc = reviewedAt,
            Score = score,
            RatingScale = 10,
            PositiveText = positiveText,
            OriginalLang = language,
            GuestName = "Anna",
            IsVisible = isVisible
        };

    private static DateTimeOffset Date(int year, int month, int day)
        => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private sealed class TestReviewsDbContextFactory(DbContextOptions<RappReviewsDbContext> options)
        : IDbContextFactory<RappReviewsDbContext>
    {
        public RappReviewsDbContext CreateDbContext() => new(options);

        public Task<RappReviewsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
