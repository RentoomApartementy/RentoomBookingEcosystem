using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.Upsell;
using RentoomBooking.SharedClasses.Services.Upsell;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace RentoomBooking.Api.Upsell;

public class UpsellVouchersFunction
{
    private readonly IUpsellVoucherRedeemService _upsellVoucherRedeemService;
    private readonly ILogger<UpsellVouchersFunction> _logger;

    public UpsellVouchersFunction(IUpsellVoucherRedeemService upsellVoucherRedeemService, ILogger<UpsellVouchersFunction> logger)
    {
        _upsellVoucherRedeemService = upsellVoucherRedeemService ?? throw new ArgumentNullException(nameof(upsellVoucherRedeemService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("ValidateUpsellVoucher")]
    public Task<HttpResponseData> ValidateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/upsells/vouchers/validate")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // TODO: Secure endpoint with partner auth and rate limiting before production rollout.
        return ProcessVoucherRequestAsync(req, VoucherAction.Validate, cancellationToken);
    }

    [Function("RedeemUpsellVoucher")]
    public Task<HttpResponseData> RedeemAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/upsells/vouchers/redeem")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        // TODO: Secure endpoint with partner auth and rate limiting before production rollout.
        return ProcessVoucherRequestAsync(req, VoucherAction.Redeem, cancellationToken);
    }

    private async Task<HttpResponseData> ProcessVoucherRequestAsync(HttpRequestData req, VoucherAction action, CancellationToken cancellationToken)
    {
        var actionName = action == VoucherAction.Validate ? "ValidateUpsellVoucher" : "RedeemUpsellVoucher";
        _logger.LogInformation("{ActionName} started at: {Time}", actionName, DateTime.UtcNow);

        var response = req.CreateResponse();

        try
        {
            var body = await req.ReadAsStringAsync();
            VoucherActionRequestDto? payload;

            try
            {
                payload = JsonConvert.DeserializeObject<VoucherActionRequestDto>(body);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON payload in {ActionName}.", actionName);
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Invalid JSON payload.", cancellationToken);
                return response;
            }

            var hasCodeShort = !string.IsNullOrWhiteSpace(payload?.CodeShort);
            var hasQrToken = !string.IsNullOrWhiteSpace(payload?.QrToken);

            if (hasCodeShort == hasQrToken)
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("Exactly one of 'codeShort' or 'qrToken' must be provided.", cancellationToken);
                return response;
            }

            if (!Guid.TryParse(payload?.ReservationToken, out var reservationTokenGuid))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("'reservationToken' must be a valid GUID.", cancellationToken);
                return response;
            }

            RedeemResultDto result;
            if (action == VoucherAction.Validate)
            {
                result = hasCodeShort
                    ? await _upsellVoucherRedeemService.ValidateByCodeShortAsync(payload!.CodeShort!.Trim(), cancellationToken)
                    : await _upsellVoucherRedeemService.ValidateByQrTokenAsync(payload!.QrToken!.Trim(), cancellationToken);

                result = EnsureReservationTokenMatch(result, reservationTokenGuid);
            }
            else
            {
                var preValidation = hasCodeShort
                    ? await _upsellVoucherRedeemService.ValidateByCodeShortAsync(payload!.CodeShort!.Trim(), cancellationToken)
                    : await _upsellVoucherRedeemService.ValidateByQrTokenAsync(payload!.QrToken!.Trim(), cancellationToken);

                preValidation = EnsureReservationTokenMatch(preValidation, reservationTokenGuid);
                if (!preValidation.Success)
                {
                    result = preValidation;
                }
                else
                {
                    result = hasCodeShort
                        ? await _upsellVoucherRedeemService.TryRedeemByCodeShortAsync(payload!.CodeShort!.Trim())
                        : await _upsellVoucherRedeemService.TryRedeemByQrTokenAsync(payload!.QrToken!.Trim());

                    result = EnsureReservationTokenMatch(result, reservationTokenGuid);
                }
            }

            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(result), cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during {ActionName}.", actionName);
            response.StatusCode = HttpStatusCode.InternalServerError;
            await response.WriteStringAsync("Internal server error.", cancellationToken);
            return response;
        }
        finally
        {
            _logger.LogInformation("{ActionName} finished at: {Time}", actionName, DateTime.UtcNow);
        }
    }

    private sealed class VoucherActionRequestDto
    {
        [JsonProperty("codeShort")]
        public string? CodeShort { get; set; }

        [JsonProperty("qrToken")]
        public string? QrToken { get; set; }

        [JsonProperty("reservationToken")]
        public string? ReservationToken { get; set; }
    }

    private static RedeemResultDto EnsureReservationTokenMatch(RedeemResultDto result, Guid expectedReservationGuid)
    {
        if (result.ReservationGuid == Guid.Empty || result.ReservationGuid == expectedReservationGuid)
        {
            return result;
        }

        return new RedeemResultDto
        {
            Success = false,
            FailureReason = "ReservationTokenMismatch",
            UpdatedUsedCount = result.UpdatedUsedCount,
            MaxUses = result.MaxUses,
            ReservationGuid = result.ReservationGuid,
            PartnerServiceId = result.PartnerServiceId,
            TitleSnapshot = result.TitleSnapshot
        };
    }

    private enum VoucherAction
    {
        Validate,
        Redeem
    }
}
