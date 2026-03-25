using System.Globalization;

namespace RentoomBookingWeb.Helpers
{
    public static class NavigationHelper
    {
        public static string GetLocalizedUrl(string path, string? culture = null)
        {
            if (string.IsNullOrEmpty(path)) path = "/";
            
            // Normalize path
            if (!path.StartsWith("/")) path = "/" + path;

            // Get current culture if not provided
            if (string.IsNullOrEmpty(culture))
            {
                var currentCulture = CultureInfo.CurrentUICulture.Name; // e.g. "pl-PL"
                culture = currentCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en" : "pl";
            }

            // Ensure culture is either 'pl' or 'en'
            if (culture != "pl" && culture != "en")
            {
                culture = "pl"; // fallback to default
            }

            // If the path already starts with a culture code, don't double it
            if (path.StartsWith("/pl/") || path == "/pl" || path.StartsWith("/en/") || path == "/en")
            {
                return path;
            }

            // Special case for root
            if (path == "/")
            {
                return $"/{culture}";
            }

            return $"/{culture}{path}";
        }

        public static string GetCultureFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "pl";

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0)
            {
                var firstSegment = segments[0].ToLower();
                if (firstSegment == "pl" || firstSegment == "en")
                {
                    return firstSegment;
                }
            }

            return "pl";
        }

        public static string GetFullCultureName(string shortCode)
        {
            return shortCode.ToLower() switch
            {
                "pl" => "pl-PL",
                "en" => "en-US",
                _ => "pl-PL"
            };
        }
        
        public static string RemoveCultureFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (segments.Count > 0)
            {
                var firstSegment = segments[0].ToLower();
                if (firstSegment == "pl" || firstSegment == "en")
                {
                    segments.RemoveAt(0);
                }
            }
            
            var newPath = "/" + string.Join("/", segments);
            return newPath == "//" ? "/" : newPath;
        }
    }
}
