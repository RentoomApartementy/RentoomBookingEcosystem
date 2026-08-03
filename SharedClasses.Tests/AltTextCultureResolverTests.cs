using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using Xunit;

namespace SharedClasses.Tests;

public class AltTextCultureResolverTests
{
    [Theory]
    [InlineData("pl-PL", "pl-PL")]
    [InlineData("en", "en")]
    [InlineData("de-DE", "de-DE")]
    [InlineData(null, "pl-PL")]
    [InlineData("", "pl-PL")]
    [InlineData("   ", "pl-PL")]
    [InlineData("!!not a culture!!", "pl-PL")]
    public void NormalizeCulture_FallsBackToDefaultForUnusableInput(string? input, string expected)
    {
        Assert.Equal(expected, AltTextCultureResolver.NormalizeCulture(input));
    }

    [Fact]
    public void SelectBest_FallsBackToDefaultForACultureNameIcuAccepts()
    {
        // ICU happily builds a custom culture for names like "xx-YY", so unknown cultures survive
        // normalization and are only resolved away by the fallback chain.
        var rows = Rows(("pl-PL", 1), ("en", 2));

        Assert.Equal("pl-PL", AltTextCultureResolver.SelectBest(rows, AltTextCultureResolver.NormalizeCulture("xx-YY"))!.Culture);
    }

    [Fact]
    public void SelectBest_PrefersExactCultureOverNeutralAndDefault()
    {
        var rows = Rows(("pl-PL", 1), ("en", 2), ("en-US", 3));

        Assert.Equal("en-US", AltTextCultureResolver.SelectBest(rows, "en-US")!.Culture);
    }

    [Fact]
    public void SelectBest_MatchesStoredNeutralWhenRequestIsSpecific()
    {
        // Requested "en-GB", the table only holds the neutral "en" used by the terms/blog tables.
        var rows = Rows(("pl-PL", 1), ("en", 2));

        Assert.Equal("en", AltTextCultureResolver.SelectBest(rows, "en-GB")!.Culture);
    }

    [Fact]
    public void SelectBest_MatchesStoredSpecificWhenRequestIsNeutral()
    {
        // Requested "de", the table only holds "de-DE" the way cookie notices store cultures.
        var rows = Rows(("pl-PL", 1), ("de-DE", 2));

        Assert.Equal("de-DE", AltTextCultureResolver.SelectBest(rows, "de")!.Culture);
    }

    [Fact]
    public void SelectBest_FallsBackToDefaultCultureWhenRequestedFamilyIsMissing()
    {
        var rows = Rows(("pl-PL", 1), ("en", 2));

        Assert.Equal("pl-PL", AltTextCultureResolver.SelectBest(rows, "it")!.Culture);
    }

    [Fact]
    public void SelectBest_FallsBackToNeutralDefaultWhenSpecificDefaultIsMissing()
    {
        var rows = Rows(("cs", 4), ("pl", 2));

        Assert.Equal("pl", AltTextCultureResolver.SelectBest(rows, "it")!.Culture);
    }

    [Fact]
    public void SelectBest_FallsBackToLowestIdWhenNothingElseMatches()
    {
        var rows = Rows(("fr", 7), ("cs", 3));

        Assert.Equal("cs", AltTextCultureResolver.SelectBest(rows, "it")!.Culture);
    }

    [Fact]
    public void SelectBest_ReturnsNullForNoRows()
    {
        Assert.Null(AltTextCultureResolver.SelectBest(Array.Empty<ApartmentMediaAltText>(), "pl-PL"));
    }

    [Fact]
    public void GetCandidateNeutralCultures_CollapsesToOneEntryForTheDefaultFamily()
    {
        Assert.Equal(new[] { "pl" }, AltTextCultureResolver.GetCandidateNeutralCultures("pl-PL"));
        Assert.Equal(new[] { "en", "pl" }, AltTextCultureResolver.GetCandidateNeutralCultures("en-US"));
    }

    private static List<ApartmentMediaAltText> Rows(params (string Culture, int Id)[] rows)
        => rows.Select(row => new ApartmentMediaAltText
        {
            Id = row.Id,
            MediaAssetId = 1,
            Culture = row.Culture,
            AltText = $"alt-{row.Culture}"
        }).ToList();
}
