using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Upsell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    /*  public interface IUpsellVoucherQueryService
      {
          Task<List<UpsellVoucherDto>> GetByReservationAsync(Guid reservationGuid);
          Task<UpsellVoucherDto?> GetByCodeShortAsync(string codeShort);
          Task<UpsellVoucherDto?> GetByQrTokenAsync(string qrToken);
      }

      public class UpsellVoucherQueryService : IUpsellVoucherQueryService
      {
          private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
          private readonly Task _initializationTask;

          public UpsellVoucherQueryService(IDbContextFactory<PostgresBookingDbContext> dbContextFactory)
          {
              _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
              _initializationTask = EnsureCreatedAsync();
          }

          public async Task<List<UpsellVoucherDto>> GetByReservationAsync(Guid reservationGuid)
          {
              // await _initializationTask;

              await using var context = _dbContextFactory.CreateDbContext();
              var results = await context.UpsellVouchers.Include(v=> v.UpsellOrderLine).
                  Where(v => v.ReservationGuid == reservationGuid).AsNoTracking().Select(v => new { voucher = v, line = v.UpsellOrderLine }).ToListAsync();




              return results
                  .Select(result => MapToDto(result.voucher, result.line, includeQrToken: true))
                  .ToList();
          }

          public async Task<UpsellVoucherDto?> GetByCodeShortAsync(string codeShort)
          {
              if (string.IsNullOrWhiteSpace(codeShort))
              {
                  return null;
              }

             // // await _initializationTask;


              await using var context = _dbContextFactory.CreateDbContext();
              var result = context.UpsellVouchers
                  .Include(v => v.UpsellOrderLine)
                  .AsNoTracking()
                  .Where(v => v.CodeShort == codeShort)
                  .Select(v => new { voucher = v, line = v.UpsellOrderLine }).
                  FirstOrDefault();


              await (from voucher in context.UpsellVouchers.AsNoTracking()
                                  join line in context.UpsellOrderLines.AsNoTracking()
                                      on voucher.UpsellOrderLineGuid equals line.UpsellOrderLineGuid
                                  where voucher.CodeShort == codeShort
                                  select new { voucher, line })
                  .FirstOrDefaultAsync();

              return result is null ? null : MapToDto(result.voucher, result.line, includeQrToken: false);
          }

          public async Task<UpsellVoucherDto?> GetByQrTokenAsync(string qrToken)
          {
              if (string.IsNullOrWhiteSpace(qrToken))
              {
                  return null;
              }

              // await _initializationTask;

              var trimmedToken = qrToken.Trim();
              await using var context = _dbContextFactory.CreateDbContext();
              var result = context.UpsellVouchers
                  .Include(v => v.UpsellOrderLine)
                  .AsNoTracking()
                  .Where(v => v.QrToken == trimmedToken)
                  .Select(v => new { voucher = v, line = v.UpsellOrderLine })
                  .FirstOrDefault();
              await (from voucher in context.UpsellVouchers.AsNoTracking()
                                  join line in context.UpsellOrderLines.AsNoTracking()
                                      on voucher.UpsellOrderLineGuid equals line.UpsellOrderLineGuid
                                  where voucher.QrToken == trimmedToken
                                  select new { voucher, line })
                  .FirstOrDefaultAsync()

              return result is null ? null : MapToDto(result.voucher, result.line, includeQrToken: false);
          }

          private static string NormalizeCodeShort(string codeShort)
          {
              return codeShort.Trim().ToUpperInvariant().Replace("-", string.Empty);
          }

          private static UpsellVoucherDto MapToDto(
              Models.Database.EFEntitites.UpsellVoucherEntity voucher,
              Models.Database.EFEntitites.UpsellOrderLineEntity line,
              bool includeQrToken)
          {
              return new UpsellVoucherDto
              {
                  VoucherGuid = voucher.UpsellVoucherGuid,
                  OrderLineGuid = voucher.UpsellOrderLineGuid,
                  ReservationGuid = voucher.ReservationGuid,
                  PartnerServiceId = line.PartnerServiceId,
                  CodeShort = voucher.CodeShort,
                  QrToken = includeQrToken ? voucher.QrToken : null,
                  UsedCount = voucher.UsedCount,
                  MaxUses = voucher.MaxUses,
                  ValidFrom = voucher.ValidFrom,
                  ValidTo = voucher.ValidTo,
                  Status = voucher.Status,
                  TitleSnapshot = line.TitleSnapshot,
                  Currency = line.Currency
              };
          }

          private async Task EnsureCreatedAsync()
          {
              await using var context = _dbContextFactory.CreateDbContext();
              await context.Database.EnsureCreatedAsync();
          }
      }

  }*/
}
