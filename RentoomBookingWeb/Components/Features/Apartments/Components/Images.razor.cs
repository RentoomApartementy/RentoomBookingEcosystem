using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBookingWeb.Components.Features.Apartments.Components;

public partial class Images : ComponentBase
{
    [Parameter] public List<ObjectMedium>? ImagesList { get; set; }
    [Parameter] public string ApartmentName { get; set; } = string.Empty;

    [Inject] private IStringLocalizer<RentoomBookingWeb.Apartment> Localizer { get; set; } = default!;

    private string PhotoAltFor(int index) => $"{ApartmentName} {Localizer["Apartment_PhotoAltSuffix"]} {index + 1}";

    private bool _isModalOpen = false;
    private int _currentImageIndex = 0;

    private void OpenGallery(int index)
    {
        if (ImagesList == null || index < 0 || index >= ImagesList.Count) return;
        
        _currentImageIndex = index;
        _isModalOpen = true;
    }

    private void SelectImage(int index)
    {
        if (ImagesList == null || index < 0 || index >= ImagesList.Count) return;
        _currentImageIndex = index;
    }

    private void CloseGallery()
    {
        _isModalOpen = false;
    }

    private void NextImage()
    {
        if (ImagesList == null || ImagesList.Count == 0) return;
        _currentImageIndex = (_currentImageIndex + 1) % ImagesList.Count;
    }

    private void PrevImage()
    {
        if (ImagesList == null || ImagesList.Count == 0) return;
        _currentImageIndex = (_currentImageIndex - 1 + ImagesList.Count) % ImagesList.Count;
    }
}