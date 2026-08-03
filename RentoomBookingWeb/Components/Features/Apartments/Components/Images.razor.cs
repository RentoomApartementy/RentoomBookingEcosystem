using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBookingWeb.Components.Features.Apartments.Components;

public partial class Images : ComponentBase, IAsyncDisposable
{
    [Parameter] public List<ObjectMedium>? ImagesList { get; set; }
    [Parameter] public string ApartmentName { get; set; } = string.Empty;

    [Inject] private IStringLocalizer<RentoomBookingWeb.Apartment> Localizer { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Alt text authored per photo and culture wins; the generated "<name> photo N" stays as fallback.
    private string PhotoAltFor(int index)
    {
        var alt = ImagesList is not null && index >= 0 && index < ImagesList.Count
            ? ImagesList[index].Alt
            : null;

        return string.IsNullOrWhiteSpace(alt)
            ? $"{ApartmentName} {Localizer["Apartment_PhotoAltSuffix"]} {index + 1}"
            : alt;
    }

    private string MainPhotoAlt
    {
        get
        {
            var alt = ImagesList?.FirstOrDefault()?.Alt;
            return string.IsNullOrWhiteSpace(alt)
                ? string.Format(Localizer["Apartment_MainPhotoAlt"], ApartmentName)
                : alt;
        }
    }

    private bool _isModalOpen = false;
    private bool _wasModalOpen = false;
    private int _currentImageIndex = 0;

    private ElementReference _modalMainViewRef;
    private IJSObjectReference? _swipeModule;
    private IJSObjectReference? _swipeHandle;
    private DotNetObjectReference<Images>? _dotNetRef;

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

    [JSInvokable]
    public void SwipeNext()
    {
        NextImage();
        StateHasChanged();
    }

    [JSInvokable]
    public void SwipePrev()
    {
        PrevImage();
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isModalOpen && !_wasModalOpen)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            _swipeModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/imageSwipe.js");
            _swipeHandle = await _swipeModule.InvokeAsync<IJSObjectReference>("initSwipe", _dotNetRef, _modalMainViewRef);
        }
        else if (!_isModalOpen && _wasModalOpen && _swipeHandle is not null)
        {
            await _swipeHandle.InvokeVoidAsync("dispose");
            _swipeHandle = null;
        }

        _wasModalOpen = _isModalOpen;
    }

    public async ValueTask DisposeAsync()
    {
        if (_swipeHandle is not null)
        {
            try { await _swipeHandle.InvokeVoidAsync("dispose"); } catch (JSDisconnectedException) { }
        }

        _dotNetRef?.Dispose();
    }
}
