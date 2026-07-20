using RentoomBookingWeb.Services.Localization;
using Xunit;

namespace SharedClasses.Tests.Localization;

public class LanguageSwitchNavigationHelperTests
{
    [Fact]
    public void BuildTargetPath_LocalizedApartmentRoute_RewritesToSelectedLocale()
    {
        var routeService = new FakeRouteLocalizationService()
            .MapSlug("apartamenty", "pl", "Apartments")
            .MapUrl("Apartments", "en-US", "/en/apartments");

        var target = LanguageSwitchNavigationHelper.BuildTargetPath(
            "pl/apartamenty/257/slusarska-5",
            "?startDate=2026-08-01",
            "pl-PL",
            "en-US",
            routeService);

        Assert.Equal("/en/apartments/257/slusarska-5?startDate=2026-08-01", target);
    }

    [Fact]
    public void BuildTargetPath_HomeRoute_RewritesToSelectedCultureRoot()
    {
        var target = LanguageSwitchNavigationHelper.BuildTargetPath(
            string.Empty,
            string.Empty,
            "pl-PL",
            "de-DE",
            new FakeRouteLocalizationService());

        Assert.Equal("/de", target);
    }

    [Fact]
    public void BuildTargetPath_PrefixlessTechnicalRoute_RemainsPrefixless()
    {
        var target = LanguageSwitchNavigationHelper.BuildTargetPath(
            "rezerwuj/123/podsumowanie",
            string.Empty,
            "pl-PL",
            "en-US",
            new FakeRouteLocalizationService());

        Assert.Equal("/rezerwuj/123/podsumowanie", target);
    }

    [Fact]
    public void BuildTargetPath_FallbackAnyCultureSlug_RewritesUsingLocalizedPageKey()
    {
        var routeService = new FakeRouteLocalizationService()
            .MapAnySlug("apartamenty", "Apartments")
            .MapUrl("Apartments", "en-US", "/en/apartments");

        var target = LanguageSwitchNavigationHelper.BuildTargetPath(
            "pl/apartamenty/257/slusarska-5",
            string.Empty,
            "pl-PL",
            "en-US",
            routeService);

        Assert.Equal("/en/apartments/257/slusarska-5", target);
    }
}
