using System;
using System.Collections.Generic;

namespace RentoomBookingWeb.Services.Localization
{
    public record RouteDefinition(Type ComponentType, string? RouteTemplate = null);

    public static class PageMapping
    {
        public static readonly Dictionary<string, RouteDefinition> KeyToComponent = new()
        {
            ["Home"] = new(typeof(RentoomBookingWeb.Components.Features.Home.Pages.Home)),
            ["Apartments"] = new(typeof(RentoomBookingWeb.Components.Features.Apartments.Pages.Apartments), "{StartDate}/{EndDate}/{Adults}/{Children}"),
            ["AllApartments"] = new(typeof(RentoomBookingWeb.Components.Features.AllApartments.Pages.AllApartments)),
            ["ApartmentDetail"] = new(typeof(RentoomBookingWeb.Components.Features.ReservationWorkflow.Pages.ApartmentPage), "{Id}/{Slug}/{StartDate}/{EndDate}/{Adults}/{Children}"),
            ["Statute"] = new(typeof(RentoomBookingWeb.Components.Features.Statute.Pages.Statute), "{Id}/{Slug}"),
            ["Contact"] = new(typeof(RentoomBookingWeb.Components.Features.Contact.Pages.Contact)),
            ["Cooperation"] = new(typeof(RentoomBookingWeb.Components.Features.Cooperation.Pages.Cooperation)),
            ["AboutCity"] = new(typeof(RentoomBookingWeb.Components.Features.TorunLocation.Pages.TorunLocation))
        };
    }
}
