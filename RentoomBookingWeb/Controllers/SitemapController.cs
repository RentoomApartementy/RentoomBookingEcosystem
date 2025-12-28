using Microsoft.AspNetCore.Mvc;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System.Text;
using System.Xml.Linq;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBookingWeb.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IApartmentsService _apartmentsService;

        public SitemapController(IApartmentsService apartmentsService)
        {
            _apartmentsService = apartmentsService;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> GetSitemap()
        {
            var result = await _apartmentsService.GetAllApartmentsList();
            var apartments = result?.Items ?? new List<ApartmentObject>();

            // TU WPISZ SWOJE PODSTRONY STATYCZNE
            var staticPages = new List<string>
            {
                "/regulaminy",
                "/wspolpraca",
                "/kontakt",
                "/apartamenty",
                "/o-nas"
            };

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var sitemap = new XElement(ns + "urlset",
                
                new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}/"),
                    new XElement(ns + "changefreq", "daily"),
                    new XElement(ns + "priority", "1.0")
                ),

                new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}/apartamenty-torun"),
                    new XElement(ns + "changefreq", "daily"),
                    new XElement(ns + "priority", "0.9")
                ),

                from page in staticPages
                select new XElement(ns + "url",
                    new XElement(ns + "loc", $"{baseUrl}{page}"),
                    new XElement(ns + "changefreq", "monthly"),
                    new XElement(ns + "priority", "0.5")
                ),

                from apt in apartments
                select new XElement(ns + "url",
                    new XElement(ns + "loc", GetApartmentUrl(baseUrl, apt)),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.8")
                )
            );

            return Content(new XDeclaration("1.0", "utf-8", "yes") + Environment.NewLine + sitemap.ToString(), "application/xml", Encoding.UTF8);
        }

        private string GetApartmentUrl(string baseUrl, ApartmentObject item)
        {
            string slug = RentoomBookingWeb.Helpers.StringExtensions.ToSlug(item.Name ?? "details");
            
            return $"{baseUrl}/apartamenty/{item.Id}/{slug}";
        }
    }
}