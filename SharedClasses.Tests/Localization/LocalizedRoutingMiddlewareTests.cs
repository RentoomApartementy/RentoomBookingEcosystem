using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using RentoomBookingWeb.Services.Localization;
using Xunit;

namespace SharedClasses.Tests.Localization;

public class LocalizedRoutingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_RootRequest_RedirectsUsingAcceptLanguage()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/";
        context.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";

        var middleware = new LocalizedRoutingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/en", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task InvokeAsync_PrefixedRequest_UsesUrlCultureAndSetsCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/pl/apartamenty/257/slusarska-5";
        context.Request.Headers.AcceptLanguage = "en-US,en;q=0.9";

        var middleware = new LocalizedRoutingMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.Body.FlushAsync();

        var feature = context.Features.Get<IRequestCultureFeature>();
        Assert.NotNull(feature);
        Assert.Equal("pl-PL", feature!.RequestCulture.UICulture.Name);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(CookieRequestCultureProvider.DefaultCookieName, setCookie);
        Assert.Contains("c%3Dpl-PL%7Cuic%3Dpl-PL", setCookie);
    }
}
