namespace RentoomBooking.SharedClasses.Configuration
{
    public sealed class ApartmentMediaVariantsOptions
    {
        public const string SectionName = "ApartmentMediaVariants";

        public int CardMaxWidth { get; set; } = 800;
        public int CardMaxHeight { get; set; } = 520;
        public int CardWebpQuality { get; set; } = 75;
    }
}
