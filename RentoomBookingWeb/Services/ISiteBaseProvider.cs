using Microsoft.Extensions.Configuration;

namespace RentoomBookingWeb.Services
{
    // Provides the configured public site origin for payment and SEO URLs.
    public interface ISiteBaseProvider
    {
        Uri GetBaseUri();
        string GetAbsoluteUrl(string path);
    }

    public sealed class SiteBaseProvider : ISiteBaseProvider
    {
        private const string DefaultSiteBaseUrl = "https://rentoom.pl";
        private readonly Uri _baseUri;

        public SiteBaseProvider(IConfiguration configuration)
        {
            var configuredBaseUrl = configuration["RentoomSiteBaseUrl"];
            if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var configuredUri)
                || (configuredUri.Scheme != Uri.UriSchemeHttp && configuredUri.Scheme != Uri.UriSchemeHttps))
            {
                configuredUri = new Uri(DefaultSiteBaseUrl);
            }

            _baseUri = new Uri(configuredUri.GetLeftPart(UriPartial.Authority).TrimEnd('/'));
        }

        public Uri GetBaseUri() => _baseUri;

        public string GetAbsoluteUrl(string path)
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri)
                && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.AbsoluteUri;
            }

            return new Uri(_baseUri, path.TrimStart('/')).AbsoluteUri;
        }
    }
}
