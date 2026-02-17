using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.QrMaint
{
    public class RappQrMaintService
    {
        private readonly QrMaintRappDbContext _dbContext;

        public RappQrMaintService(QrMaintRappDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string?> GetQrMaintFormUrlAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            var items = await GetQrItemsAsync(apartmentId, cancellationToken);
            var usterki = items?.FirstOrDefault(i => string.Equals(i.RentoomCodeType, "USTERKI", StringComparison.OrdinalIgnoreCase));
            return usterki?.Message;
        }

        public async Task<RentoomWifiInfo?> GetWifiInfoAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            var items = await GetQrItemsAsync(apartmentId, cancellationToken);
            var wifiItem = items?.FirstOrDefault(i =>
                string.Equals(i.RentoomCodeType, "SIEC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(i.Type, "WIFI", StringComparison.OrdinalIgnoreCase));

            if (wifiItem == null)
                return null;

            if (string.IsNullOrWhiteSpace(wifiItem.Ssid)
                && string.IsNullOrWhiteSpace(wifiItem.Pass)
                && string.IsNullOrWhiteSpace(wifiItem.Auth))
            {
                return null;
            }

            return new RentoomWifiInfo
            {
                Ssid = wifiItem.Ssid,
                Password = wifiItem.Pass,
                Auth = wifiItem.Auth
            };
        }

        public Task<string?> GetLockCodeAsync(
            int apartmentItemId,
            CancellationToken cancellationToken = default)
        {
            return _dbContext.ApartmentItemLocalSettings
                .AsNoTracking()
                .Where(x => x.ApartmentItemId == apartmentItemId)
                .Select(x => x.TTLockId)
                .FirstOrDefaultAsync(cancellationToken);
        }


        public async Task<ApartmentItemLocalSettings?> GetApartmentItemCodesAsync(
            int apartmentItemId,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.ApartmentItemLocalSettings
                .AsNoTracking()
                .Where(x => x.ApartmentItemId == apartmentItemId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                return await _dbContext.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<RentoomQrItem>?> GetQrItemsAsync(int apartmentId, CancellationToken cancellationToken)
        {
            var mapping = await _dbContext.QRMaintIdosellMapping
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IDOSellApartmentId == apartmentId
                    && x.QRMaintApartmentType == "APARTAMENTY",
                    cancellationToken);

            if (mapping == null)
                return null;

            var qr = await _dbContext.RentoomQRs
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApartmentItemId == mapping.QRMaintId, cancellationToken);

            if (qr == null || string.IsNullOrEmpty(qr.QrCodesJson))
                return null;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<RentoomQrItem>>(qr.QrCodesJson, options);
        }

        public class RentoomQrItem
        {
            [JsonPropertyName("Message")]
            public string? Message { get; set; }

            [JsonPropertyName("RentoomCodeType")]
            public string? RentoomCodeType { get; set; }

            [JsonPropertyName("Type")]
            public string? Type { get; set; }

            [JsonPropertyName("ssid")]
            public string? Ssid { get; set; }

            [JsonPropertyName("pass")]
            public string? Pass { get; set; }

            [JsonPropertyName("auth")]
            public string? Auth { get; set; }

            [JsonPropertyName("size")]
            public int? Size { get; set; }
        }
    }
}
