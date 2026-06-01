using System;
using System.Collections.Generic;

namespace RentoomBookingWeb.Services.Localization
{
    public static class PageMapping
    {
        public static readonly Dictionary<string, Type> KeyToComponent = new()
        {
            ["Home"] = typeof(RentoomBookingWeb.Components.Features.Home.Pages.Home),
            ["Apartments"] = typeof(RentoomBookingWeb.Components.Features.Apartments.Pages.Apartments),
            ["AllApartments"] = typeof(RentoomBookingWeb.Components.Features.AllApartments.Pages.AllApartments),
            ["ApartmentDetail"] = typeof(RentoomBookingWeb.Components.Features.ReservationWorkflow.Pages.ApartmentPage),
            ["Statute"] = typeof(RentoomBookingWeb.Components.Features.Statute.Pages.Statute),
            ["Contact"] = typeof(RentoomBookingWeb.Components.Features.Contact.Pages.Contact),
            ["Cooperation"] = typeof(RentoomBookingWeb.Components.Features.Cooperation.Pages.Cooperation),
            ["AboutCity"] = typeof(RentoomBookingWeb.Components.Features.TorunLocation.Pages.TorunLocation)
        };
    }
}
