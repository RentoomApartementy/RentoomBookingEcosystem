using Microsoft.JSInterop;
using System.Text.Json;
namespace RentoomBooking.StayWell.Services;

public class BitrixService
{
    private readonly IJSRuntime _js;

    public BitrixService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        await _js.InvokeVoidAsync("bitrixInterop.init");
    }

    public async Task EnableLoaderAsync(string loaderUrl)
    {
        await _js.InvokeVoidAsync("bitrixInterop.enableLoader", loaderUrl);
    }

    public async Task OpenChatAsync(BitrixGuestData data)
    {
        var displayName = data.Name ?? "Gość";
        var resKey = data.ExtraInfo.Keys.FirstOrDefault(k => k.ToLower().Contains("rezerwacj"));
        if (resKey != null) displayName += $" (#{data.ExtraInfo[resKey]})";

        var gridItems = new List<object>
        {
            new { NAME = "ID Klienta", VALUE = data.ExternalId, DISPLAY = "LINE"},
            new { NAME = "Telefon", VALUE = data.Phone, DISPLAY = "LINE" },
            new { NAME = "E-mail", VALUE = data.Email, DISPLAY = "LINE" }
        };

        foreach (var item in data.ExtraInfo)
        {
            gridItems.Add(new { NAME = item.Key, VALUE = item.Value, DISPLAY = "LINE" });
        }

        var payload = new object[]
        {
            new { USER = new { NAME = displayName, EMAIL = data.Email, PERSONAL_PHONE = data.Phone } },
            new { GRID = gridItems }
        };

        var json = JsonSerializer.Serialize(payload);
        await _js.InvokeVoidAsync("bitrixInterop.openChat", json);
    }

    public async Task DestroyAsync()
    {
        await _js.InvokeVoidAsync("bitrixInterop.destroy");
    }
}
public class BitrixGuestData
{
    public string Name { get; set; } = "Gość";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ExternalId { get; set; } = "";

    public Dictionary<string, string> ExtraInfo { get; set; } = new();
}
