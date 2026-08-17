namespace RentoomBookingWeb.Configuration;

public sealed class CooperationHeroOptions
{
    public const string SectionName = "CooperationHero";
    public const string DefaultPhotoKey = "photo_1";

    public string SelectedPhoto { get; set; } = DefaultPhotoKey;

    public Dictionary<string, string> Photos { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string ResolveSelectedPhoto()
    {
        if (!string.IsNullOrWhiteSpace(SelectedPhoto)
            && Photos.TryGetValue(SelectedPhoto.Trim(), out var selectedUrl)
            && !string.IsNullOrWhiteSpace(selectedUrl))
        {
            return selectedUrl;
        }

        if (Photos.TryGetValue(DefaultPhotoKey, out var fallbackUrl)
            && !string.IsNullOrWhiteSpace(fallbackUrl))
        {
            return fallbackUrl;
        }

        return "/assets/images/cooperation/forest_apartment.jpg";
    }
}
