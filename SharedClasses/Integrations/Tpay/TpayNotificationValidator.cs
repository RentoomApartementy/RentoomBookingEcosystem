using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Integrations.Tpay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.Tpay
{
    public interface ITpayNotificationValidator
    {
        Task<bool> ValidateJwsAsync(string? jwsSignatureHeader, string payload, CancellationToken cancellationToken);
        bool ValidateMd5(TpayTransactionSettlementNotification notification);
    }

    public class TpayNotificationValidator : ITpayNotificationValidator
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TpaySettings _settings;
        private readonly ILogger<TpayNotificationValidator> _logger;
        private X509Certificate2? _rootCertificate;

        public TpayNotificationValidator(
            IHttpClientFactory httpClientFactory,
            IOptions<TpaySettings> options,
            ILogger<TpayNotificationValidator> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> ValidateJwsAsync(string? jwsSignatureHeader, string payload, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jwsSignatureHeader))
            {
                _logger.LogWarning("Missing X-JWS-Signature header on Tpay notification.");
                return false;
            }

            var segments = jwsSignatureHeader.Split('.');
            if (segments.Length != 3)
            {
                _logger.LogWarning("Invalid JWS format on Tpay notification.");
                return false;
            }

            var headerSegment = segments[0];
            var signatureSegment = segments[2];

            var headerJson = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(headerSegment));
            var header = System.Text.Json.JsonDocument.Parse(headerJson).RootElement;
            if (!header.TryGetProperty("x5u", out var x5uElement))
            {
                _logger.LogWarning("JWS header missing x5u.");
                return false;
            }

            var x5u = x5uElement.GetString();
            if (string.IsNullOrWhiteSpace(x5u))
            {
                _logger.LogWarning("JWS x5u is empty.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_settings.JwsCertPrefix) && !x5u.StartsWith(_settings.JwsCertPrefix, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("JWS certificate URL {X5U} does not match expected prefix {Prefix}.", x5u, _settings.JwsCertPrefix);
                return false;
            }

            var certificate = await DownloadCertificateAsync(x5u, cancellationToken);
            if (certificate is null)
            {
                return false;
            }

            if (!(await ValidateCertificateChainAsync(certificate, cancellationToken)))
            {
                _logger.LogWarning("JWS certificate chain validation failed.");
                return false;
            }

            var signingInput = Encoding.ASCII.GetBytes($"{headerSegment}.{WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}");
            var signatureBytes = WebEncoders.Base64UrlDecode(signatureSegment);

            using var rsa = certificate.GetRSAPublicKey();
            if (rsa is null)
            {
                _logger.LogWarning("No RSA public key found in JWS certificate.");
                return false;
            }

            var verified = rsa.VerifyData(signingInput, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!verified)
            {
                _logger.LogWarning("JWS signature verification failed for Tpay notification.");
            }

            return verified;
        }

        public bool ValidateMd5(TpayTransactionSettlementNotification notification)
        {
            if (notification is null) throw new ArgumentNullException(nameof(notification));

            if (string.IsNullOrWhiteSpace(_settings.MerchantSecurityCode))
            {
                _logger.LogWarning("Tpay MerchantSecurityCode is not configured; skipping md5 validation.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(notification.id) ||
                string.IsNullOrWhiteSpace(notification.tr_id) ||
                string.IsNullOrWhiteSpace(notification.tr_amount) ||
                string.IsNullOrWhiteSpace(notification.tr_crc) ||
                string.IsNullOrWhiteSpace(notification.md5sum))
            {
                _logger.LogWarning("Missing fields for md5 validation in Tpay notification.");
                return false;
            }

            var toHash = string.Concat(notification.id, notification.tr_id, notification.tr_amount, notification.tr_crc, _settings.MerchantSecurityCode);
            using var md5 = MD5.Create();
            var computed = md5.ComputeHash(Encoding.UTF8.GetBytes(toHash));
            var computedHex = BitConverter.ToString(computed).Replace("-", string.Empty).ToLowerInvariant();

            var isValid = string.Equals(computedHex, notification.md5sum, StringComparison.OrdinalIgnoreCase);
            if (!isValid)
            {
                _logger.LogWarning("Invalid md5sum for transaction {TransactionId}. Expected {Expected} but got {Actual}.", notification.tr_id, computedHex, notification.md5sum);
            }

            return isValid;
        }

        private async Task<X509Certificate2?> DownloadCertificateAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var data = await client.GetByteArrayAsync(url, cancellationToken);
                return new X509Certificate2(data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download JWS certificate from {Url}.", url);
                return null;
            }
        }

        private async Task<bool> ValidateCertificateChainAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
        {
            try
            {
                if (_rootCertificate is null && !string.IsNullOrWhiteSpace(_settings.RootCaPemUrl))
                {
                    var client = _httpClientFactory.CreateClient();
                    var pem = await client.GetStringAsync(_settings.RootCaPemUrl, cancellationToken);
                    _rootCertificate = X509Certificate2.CreateFromPem(pem);
                }

                using var chain = new X509Chain();

                chain.ChainPolicy.ExtraStore.Add(certificate);

                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                if (_rootCertificate is not null)
                {
                    //chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    //chain.ChainPolicy.CustomTrustStore.Add(_rootCertificate);
                    
                  //  chain.ChainPolicy.ExtraStore.Add(_roo);
                }

                return chain.Build(_rootCertificate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate certificate chain for Tpay notification.");
                return false;
            }
        }
    }
}
