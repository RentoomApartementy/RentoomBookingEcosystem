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

            RedeemResultDto result;
            if (hasCodeShort)
            {
                var codeShort = payload!.CodeShort!.Trim();
                result = action == VoucherAction.Validate
                    ? await _upsellVoucherRedeemService.ValidateByCodeShortAsync(codeShort, cancellationToken)
                    : await _upsellVoucherRedeemService.TryRedeemByCodeShortAsync(codeShort);
            }
            else
            {
                var qrToken = payload!.QrToken!.Trim();
                result = action == VoucherAction.Validate
                    ? await _upsellVoucherRedeemService.ValidateByQrTokenAsync(qrToken, cancellationToken)
                    : await _upsellVoucherRedeemService.TryRedeemByQrTokenAsync(qrToken);
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
    }

    private enum VoucherAction
    {
        Validate,
        Redeem
    }
}
