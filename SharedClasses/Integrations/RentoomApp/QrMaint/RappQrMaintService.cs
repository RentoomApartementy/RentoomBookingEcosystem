using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
            var items = JsonSerializer.Deserialize<List<RentoomQrItem>>(qr.QrCodesJson, options);
            var usterki = items?.FirstOrDefault(i => i.RentoomCodeType == "USTERKI");
            return usterki?.Message;
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

        public class RentoomQrItem
        {
            [JsonPropertyName("Message")]
            public string? Message { get; set; }

            [JsonPropertyName("RentoomCodeType")]
            public string? RentoomCodeType { get; set; }
        }
    }
}
