namespace RentoomBookingWeb.Services;

public sealed class CanonicalHostRedirectMiddleware
{
    private const string CanonicalHost = "rentoom.pl";
    private const string RedirectedHost = "www.rentoom.pl";
    private readonly RequestDelegate _next;

    public CanonicalHostRedirectMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISiteBaseProvider siteBaseProvider)
    {
        if (string.Equals(context.Request.Host.Host, RedirectedHost, StringComparison.OrdinalIgnoreCase)
            && string.Equals(siteBaseProvider.GetBaseUri().Host, CanonicalHost, StringComparison.OrdinalIgnoreCase))
        {
            var target = siteBaseProvider.GetAbsoluteUrl(
                $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
            context.Response.Redirect(target, permanent: true);
            return;
        }

        await _next(context);
    }
}
