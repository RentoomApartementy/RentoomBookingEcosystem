using Microsoft.AspNetCore.Components;

namespace RentoomBookingWeb.Services
{
    //aby uzyskac podstawowy URI strony dla Tpay (bo w settings dla tpay jest tylko relaetive path dla successurl i errorurl itp.. )
    public interface ISiteBaseProvider
    {
        Uri GetBaseUri();
    }

    public sealed class SiteBaseProvider : ISiteBaseProvider
    {
        private readonly NavigationManager _nav;
        private readonly IHttpContextAccessor _hca;

        public SiteBaseProvider(NavigationManager nav, IHttpContextAccessor hca)
        {
            _nav = nav;
            _hca = hca;
        }

        public Uri GetBaseUri()
        {
            var ctx = _hca.HttpContext;
            if (ctx != null)
                return new Uri($"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}");

            // works during Blazor interactive flow when HttpContext may be null
            return new Uri(_nav.BaseUri);
        }
    }
}
