using Microsoft.AspNetCore.Mvc;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedFrontend.Localization;
using RentoomBookingWeb.Services.Localization;

namespace RentoomBookingWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IIdoApartmentService _idoApartmentService;
        private readonly IRouteLocalizationService _routeService;

        public SitemapController(IApartmentsService apartmentsService, IIdoApartmentService idoApartmentService, IRouteLocalizationService routeService)
        {
            _apartmentsService = apartmentsService;
            _idoApartmentService = idoApartmentService;
            _routeService = routeService;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> GetSitemap()
        {
            var result = await _apartmentsService.GetAllApartmentsList();
            var apartments = result?.Items ?? new List<ApartmentObject>();

            var staticPageKeys = new List<string>
            {
                "Statute",
                "Cooperation",
                "Contact",
                "Apartments",
                "Home"
            };

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace xhtml = "http://www.w3.org/1999/xhtml";

            var urlElements = new List<XElement>();

            // Static pages with hreflangs
            foreach (var pageKey in staticPageKeys)
            {
                foreach (var culture in SupportedLanguagesProvider.SupportedCultureNames)
                {
                    var loc = $"{baseUrl}{_routeService.GetLocalizedUrl(pageKey, culture)}";
                    var urlEl = new XElement(ns + "url",
                        new XElement(ns + "loc", loc),
                        new XElement(ns + "changefreq", "monthly"),
                        new XElement(ns + "priority", pageKey == "Home" ? "1.0" : "0.5")
                    );

                    // Add hreflang alternates
                    foreach (var altCulture in SupportedLanguagesProvider.SupportedCultureNames)
                    {
                        urlEl.Add(new XElement(xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", altCulture),
                            new XAttribute("href", $"{baseUrl}{_routeService.GetLocalizedUrl(pageKey, altCulture)}")
                        ));
                    }
                    
                    urlEl.Add(new XElement(xhtml + "link",
                        new XAttribute("rel", "alternate"),
                        new XAttribute("hreflang", "x-default"),
                        new XAttribute("href", $"{baseUrl}{_routeService.GetLocalizedUrl(pageKey, SupportedLanguagesProvider.DefaultCultureName)}")
                    ));

                    urlElements.Add(urlEl);
                }
            }

            // Apartments with hreflangs
            foreach (var apt in apartments)
            {
                foreach (var culture in SupportedLanguagesProvider.SupportedCultureNames)
                {
                    var loc = GetApartmentUrl(baseUrl, apt, culture);
                    var urlEl = new XElement(ns + "url",
                        new XElement(ns + "loc", loc),
                        new XElement(ns + "changefreq", "weekly"),
                        new XElement(ns + "priority", "0.8")
                    );

                    // Add hreflang alternates
                    foreach (var altCulture in SupportedLanguagesProvider.SupportedCultureNames)
                    {
                        urlEl.Add(new XElement(xhtml + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", altCulture),
                            new XAttribute("href", GetApartmentUrl(baseUrl, apt, altCulture))
                        ));
                    }
                    
                    urlEl.Add(new XElement(xhtml + "link",
                        new XAttribute("rel", "alternate"),
                        new XAttribute("hreflang", "x-default"),
                        new XAttribute("href", GetApartmentUrl(baseUrl, apt, SupportedLanguagesProvider.DefaultCultureName))
                    ));

                    urlElements.Add(urlEl);
                }
            }

            var sitemap = new XElement(ns + "urlset", 
                new XAttribute(XNamespace.Xmlns + "xhtml", xhtml),
                urlElements);

            return Content(new XDeclaration("1.0", "utf-8", "yes") + Environment.NewLine + sitemap.ToString(), "application/xml", Encoding.UTF8);
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
                aptSb.AppendLine($"- **Link:** {GetApartmentUrl(baseUrl, apt)}");
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

        private string GetApartmentUrl(string baseUrl, ApartmentObject item, string? culture = null)
        {
            string slug = RentoomBookingWeb.Helpers.StringExtensions.ToSlug(item.Name ?? "details");
            var path = $"/apartamenty/{item.Id}/{slug}";
            
            if (culture != null)
            {
                path = _routeService.GetUrlWithCulture(path, culture);
            }
            
            return $"{baseUrl}{path}";
        }
    }
}
