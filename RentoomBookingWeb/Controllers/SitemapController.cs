using Microsoft.AspNetCore.Mvc;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBookingWeb.Services.Localization;
using RentoomBooking.SharedClasses.Services.Blog;
using RentoomBooking.SharedFrontend.Localization;
using System.Globalization;
using RentoomBookingWeb.Services;

namespace RentoomBookingWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IIdoApartmentService _idoApartmentService;
        private readonly IRouteLocalizationService _routeService;
        private readonly IBlogContentReader _blogContentReader;
        private readonly FeatureFlagsService _featureFlags;

        public SitemapController(
            IApartmentsService apartmentsService, 
            IIdoApartmentService idoApartmentService,
            IRouteLocalizationService routeService,
            IBlogContentReader blogContentReader,
            FeatureFlagsService featureFlags)
        {
            _apartmentsService = apartmentsService;
            _idoApartmentService = idoApartmentService;
            _routeService = routeService;
            _blogContentReader = blogContentReader;
            _featureFlags = featureFlags;
        }

        [Route("sitemap.xml")]
        public IActionResult GetSitemapIndex()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var cultures = SupportedLanguagesProvider.SupportedCultureNames;
            
            var sitemapIndex = new XElement(ns + "sitemapindex",
                from culture in cultures
                let shortCode = culture.Split('-')[0].ToLowerInvariant()
                select new XElement(ns + "sitemap",
                    new XElement(ns + "loc", $"{baseUrl}/{shortCode}/sitemap.xml")
                )
            );

            return Content(new XDeclaration("1.0", "utf-8", "yes") + Environment.NewLine + sitemapIndex.ToString(), "application/xml", Encoding.UTF8);
        }

        [Route("{culture}/sitemap.xml")]
        public async Task<IActionResult> GetLanguageSitemap(string culture)
        {
            var supportedCultures = SupportedLanguagesProvider.SupportedCultureNames;
            var currentCulture = supportedCultures.FirstOrDefault(c => 
                string.Equals(c.Split('-')[0], culture, StringComparison.OrdinalIgnoreCase) || 
                string.Equals(c, culture, StringComparison.OrdinalIgnoreCase));

            if (currentCulture == null) return NotFound();

            var result = await _apartmentsService.GetAllApartmentsList();
            var apartments = result?.Items ?? new List<ApartmentObject>();
            var isBlogEnabled = _featureFlags.FeatureAllowed("blog");
            var blogPosts = isBlogEnabled
                ? await _blogContentReader.GetAllPublishedPostsAsync(currentCulture)
                : Array.Empty<BlogPostListItem>();

            var staticPageKeys = new List<string>
            {
                "Statute",
                "Cooperation",
                "Contact",
                "AboutCity",
                "AllApartments"
            };

            if (isBlogEnabled)
            {
                staticPageKeys.Add("BlogList");
            }

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace xhtml = "http://www.w3.org/1999/xhtml";

            var urlElements = new List<XElement>();

            // 1. Home Page
            urlElements.Add(CreateUrlElement(ns, xhtml, baseUrl, "Home", currentCulture, supportedCultures, "1.0", "daily"));

            // 2. Static Pages
            foreach (var key in staticPageKeys)
            {
                urlElements.Add(CreateUrlElement(ns, xhtml, baseUrl, key, currentCulture, supportedCultures, "0.7", "monthly"));
            }

            // 3. Apartments
            foreach (var apt in apartments)
            {
                urlElements.Add(CreateApartmentUrlElement(ns, xhtml, baseUrl, apt, currentCulture, supportedCultures, "0.8", "weekly"));
            }

            // 4. Blog Posts
            foreach (var post in blogPosts)
            {
                urlElements.Add(CreateBlogPostUrlElement(ns, xhtml, baseUrl, post, currentCulture, supportedCultures, "0.6", "weekly"));
            }

            var sitemap = new XElement(ns + "urlset", 
                new XAttribute(XNamespace.Xmlns + "xhtml", xhtml.NamespaceName),
                urlElements
            );

            return Content(new XDeclaration("1.0", "utf-8", "yes") + Environment.NewLine + sitemap.ToString(), "application/xml", Encoding.UTF8);
        }

        private XElement CreateUrlElement(XNamespace ns, XNamespace xhtml, string baseUrl, string pageKey, string currentCulture, IEnumerable<string> allCultures, string priority, string freq)
        {
            var loc = pageKey == "Home" 
                ? $"{baseUrl}/{currentCulture.Split('-')[0].ToLowerInvariant()}" 
                : $"{baseUrl}{_routeService.GetLocalizedUrl(pageKey, currentCulture)}";

            var urlElement = new XElement(ns + "url",
                new XElement(ns + "loc", loc),
                new XElement(ns + "changefreq", freq),
                new XElement(ns + "priority", priority)
            );

            foreach (var cult in allCultures)
            {
                var altLoc = pageKey == "Home"
                    ? $"{baseUrl}/{cult.Split('-')[0].ToLowerInvariant()}"
                    : $"{baseUrl}{_routeService.GetLocalizedUrl(pageKey, cult)}";

                urlElement.Add(new XElement(xhtml + "link",
                    new XAttribute("rel", "alternate"),
                    new XAttribute("hreflang", cult.Split('-')[0].ToLowerInvariant()),
                    new XAttribute("href", altLoc)
                ));
            }

            return urlElement;
        }

        private XElement CreateApartmentUrlElement(XNamespace ns, XNamespace xhtml, string baseUrl, ApartmentObject apt, string currentCulture, IEnumerable<string> allCultures, string priority, string freq)
        {
            var slug = RentoomBookingWeb.Helpers.StringExtensions.ToSlug(apt.Name ?? "details");
            var localizedBase = _routeService.GetLocalizedUrl("ApartmentDetail", currentCulture);
            var loc = $"{baseUrl}{localizedBase}/{apt.Id}/{slug}";

            var urlElement = new XElement(ns + "url",
                new XElement(ns + "loc", loc),
                new XElement(ns + "changefreq", freq),
                new XElement(ns + "priority", priority)
            );

            foreach (var cult in allCultures)
            {
                var cultLocalizedBase = _routeService.GetLocalizedUrl("ApartmentDetail", cult);
                var altLoc = $"{baseUrl}{cultLocalizedBase}/{apt.Id}/{slug}";

                urlElement.Add(new XElement(xhtml + "link",
                    new XAttribute("rel", "alternate"),
                    new XAttribute("hreflang", cult.Split('-')[0].ToLowerInvariant()),
                    new XAttribute("href", altLoc)
                ));
            }

            return urlElement;
        }

        private XElement CreateBlogPostUrlElement(XNamespace ns, XNamespace xhtml, string baseUrl, BlogPostListItem post, string currentCulture, IEnumerable<string> allCultures, string priority, string freq)
        {
            var localizedBase = _routeService.GetLocalizedUrl("BlogPost", currentCulture);
            var loc = $"{baseUrl}{localizedBase}/{post.PublicId:D}/{post.Slug}";

            var urlElement = new XElement(ns + "url",
                new XElement(ns + "loc", loc),
                new XElement(ns + "changefreq", freq),
                new XElement(ns + "priority", priority)
            );

            foreach (var cult in allCultures)
            {
                var cultLocalizedBase = _routeService.GetLocalizedUrl("BlogPost", cult);
                var altLoc = $"{baseUrl}{cultLocalizedBase}/{post.PublicId:D}/{post.Slug}";

                urlElement.Add(new XElement(xhtml + "link",
                    new XAttribute("rel", "alternate"),
                    new XAttribute("hreflang", cult.Split('-')[0].ToLowerInvariant()),
                    new XAttribute("href", altLoc)
                ));
            }

            return urlElement;
        }
        
        [Route("llms.txt")]
        [ResponseCache(Duration = 3600)]
        public async Task<IActionResult> GetLlmsTxt()
        {
            var result = await _apartmentsService.GetAllApartmentsList();
            var apartments = result?.Items ?? new List<ApartmentObject>();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var sb = new StringBuilder();
            sb.AppendLine("# Rentoom - Apartamenty w Toruniu");
            sb.AppendLine("## Lista Apartamentów");
            sb.AppendLine();

            var tasks = apartments.Select(async apt => 
            {
                string description = "Komfortowy apartament w Toruniu. Kliknij w link, aby zobaczyć zdjęcia i szczegóły.";
        
                try 
                {
                    var descriptions = await _idoApartmentService.GetObjectDescriptionsAsync(apt.Id, "pl");
                    var descObj = descriptions?.FirstOrDefault();
            
                    if (!string.IsNullOrWhiteSpace(descObj?.ShortDescription))
                    {
                        string clean = Regex.Replace(descObj.ShortDescription, "<.*?>", " ");
                        clean = System.Net.WebUtility.HtmlDecode(clean);
                        clean = Regex.Replace(clean, @"\s+", " ").Trim();
                        if (clean.Length > 350) clean = clean.Substring(0, 347) + "...";
                        description = clean;
                    }
                }
                catch
                {
                }

                var aptSb = new StringBuilder();
                aptSb.AppendLine($"### {apt.Name}");
                aptSb.AppendLine($"- **Opis:** {description}");
                aptSb.AppendLine($"- **ID:** {apt.Id}");
                
                var slug = RentoomBookingWeb.Helpers.StringExtensions.ToSlug(apt.Name ?? "details");
                var localizedBase = _routeService.GetLocalizedUrl("ApartmentDetail", "pl-PL");
                var aptUrl = $"{baseUrl}{localizedBase}/{apt.Id}/{slug}";

                aptSb.AppendLine($"- **Link:** {aptUrl}");
                aptSb.AppendLine();
        
                return aptSb.ToString();
            });

            var processedApartments = await Task.WhenAll(tasks);

            foreach (var aptText in processedApartments)
            {
                sb.Append(aptText);
            }

            sb.AppendLine("## Dane kontaktowe");

            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }
    }
}
