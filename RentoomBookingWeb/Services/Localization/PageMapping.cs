using System;
using System.Collections.Generic;

namespace RentoomBookingWeb.Services.Localization
{
    public record RouteDefinition(Type ComponentType, string? RouteTemplate = null, int Priority = 0, int MinRequiredSegments = 0);

    public static class PageMapping
    {
        public static readonly Dictionary<string, RouteDefinition> KeyToComponent = new()
        {
            ["Home"] = new(typeof(RentoomBookingWeb.Components.Features.Home.Pages.Home), Priority: 0),
            ["HomeAbTest"] = new(typeof(RentoomBookingWeb.Components.Features.Home.Pages.HomeAbTest), Priority: 0),
            ["Apartments"] = new(typeof(RentoomBookingWeb.Components.Features.Apartments.Pages.Apartments), "{StartDate}/{EndDate}/{Adults}/{Children}", Priority: 5, MinRequiredSegments: 0),
            ["AllApartments"] = new(typeof(RentoomBookingWeb.Components.Features.AllApartments.Pages.AllApartments), Priority: 0),
            ["ApartmentDetail"] = new(typeof(RentoomBookingWeb.Components.Features.ReservationWorkflow.Pages.ApartmentPage), "{Id}/{Slug}/{StartDate}/{EndDate}/{Adults}/{Children}", Priority: 10, MinRequiredSegments: 1),
            ["Statute"] = new(typeof(RentoomBookingWeb.Components.Features.Statute.Pages.Statute), "{Id}/{Slug}", Priority: 10, MinRequiredSegments: 0), // Statute is special, ID is optional for main list
            ["Contact"] = new(typeof(RentoomBookingWeb.Components.Features.Contact.Pages.Contact), Priority: 0),
            ["Cooperation"] = new(typeof(RentoomBookingWeb.Components.Features.Cooperation.Pages.Cooperation), Priority: 0),
            ["AboutCity"] = new(typeof(RentoomBookingWeb.Components.Features.TorunLocation.Pages.TorunLocation), Priority: 0)
        };
    }
}
