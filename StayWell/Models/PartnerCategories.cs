public enum PartnerCategories
{
    Empty,
    Food,
    Sweet,
    Fun,
    Cafe,
}

public static class PartnerCategory
{
    public static string ToSVGPath(this PartnerCategories category)
    {
        return category switch
        {
            PartnerCategories.Empty => "",
            PartnerCategories.Food => "/icons/chef-hat.svg",
            PartnerCategories.Sweet => "/icons/cake-slice.svg",
            PartnerCategories.Fun => "/icons/ferris-wheel.svg",
            PartnerCategories.Cafe => "/icons/coffee.svg",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Such value doesn't exist.")
        };
    }
}