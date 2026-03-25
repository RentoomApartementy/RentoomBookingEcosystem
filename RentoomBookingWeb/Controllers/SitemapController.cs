using Microsoft.AspNetCore.Mvc;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.IdoBooking;

namespace RentoomBookingWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IApartmentsService _apartmentsService;
        private readonly IIdoApartmentService _idoApartmentService;

        public SitemapController(IApartmentsService apartmentsService, IIdoApartmentService idoApartmentService)
        {
            _apartmentsService = apartmentsService;
            _idoApartmentService = idoApartmentService;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> GetSitemap()
        {
            var result = await _apartmentsService.GetAllApartmentsList();
            var apartments = result?.Items ?? new List<ApartmentObject>();

            var staticPages = new List<string>
            {
                "/regulaminy",
                "/wspolpraca",
                "/kontakt",
                "/apartamenty",
                "/o-nas",
                "/apartamenty-torun"
            };

            var supportedCultures = new[] { "pl", "en" };
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var urls = new List<XElement>();

            foreach (var culture in supportedCultures)
            {
                // Root
                urls.Add(new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}/{culture}"),
                    new XElement(ns + "changefreq", "daily"),
                    new XElement(ns + "priority", "1.0")
                ));

                // Static pages
                foreach (var page in staticPages)
                {
                    urls.Add(new XElement(ns + "url",
                        new XElement(ns + "loc", $"{baseUrl}/{culture}{page}"),
                        new XElement(ns + "changefreq", "monthly"),
                        new XElement(ns + "priority", "0.5")
                    ));
                }

                // Apartment pages
                foreach (var apt in apartments)
                {
                    urls.Add(new XElement(ns + "url",
                        new XElement(ns + "loc", GetApartmentUrl(baseUrl, apt, culture)),
                        new XElement(ns + "changefreq", "weekly"),
                        new XElement(ns + "priority", "0.8")
                    ));
                }
            }

            var sitemap = new XElement(ns + "urlset", urls);

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
                aptSb.AppendLine($"- **Link:** {GetApartmentUrl(baseUrl, apt, "pl")}");
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

        private string GetApartmentUrl(string baseUrl, ApartmentObject item, string culture = "pl")
        {
            string slug = RentoomBookingWeb.Helpers.StringExtensions.ToSlug(item.Name ?? "details");
            
            return $"{baseUrl}/{culture}/apartamenty/{item.Id}/{slug}";
        }
    }
}
