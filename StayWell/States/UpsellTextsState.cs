using System.Reflection;
using Microsoft.Extensions.Localization;
using RentoomBooking.SharedFrontend.Components.Shared.UpsellComponents;

namespace RentoomBooking.StayWell.States;

public class UpsellTextsState
{
    public UpsellTextConfig Build(IStringLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        var texts = new UpsellTextConfig();

        foreach (var property in typeof(UpsellTextConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.PropertyType != typeof(string))
            {
                continue;
            }

            var localized = localizer[property.Name];
            if (!localized.ResourceNotFound && !string.IsNullOrWhiteSpace(localized.Value))
            {
                property.SetValue(texts, localized.Value);
            }
        }

        return texts;
    }
}
