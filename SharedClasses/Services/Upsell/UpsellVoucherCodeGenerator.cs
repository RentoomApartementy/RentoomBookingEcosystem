using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.Upsell
{
    public interface IUpsellVoucherCodeGenerator
    {
        string GenerateQrToken();
        string DeriveShortCode(string qrToken);
    }

    public class UpsellVoucherCodeGenerator : IUpsellVoucherCodeGenerator
    {
        private const int QrTokenSizeBytes = 16;
        private const int ShortCodePayloadLength = 8;
        private const int ChecksumLength = 2;
        private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford's Base32 alphabet without I, L, O, U for better readability

        public string GenerateQrToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(QrTokenSizeBytes);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        public string DeriveShortCode(string qrToken)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
            {
                throw new ArgumentException("QR token must be provided.", nameof(qrToken));
            }

            var qrBytes = WebEncoders.Base64UrlDecode(qrToken);
            var hash = SHA256.HashData(qrBytes);
            var payload = EncodeCrockfordBase32(hash.AsSpan(0, 5), ShortCodePayloadLength);
            var checksum = ComputeChecksum(payload);

            return $"RW-{payload[..4]}-{payload[4..]}-{checksum}";
        }

        private static string EncodeCrockfordBase32(ReadOnlySpan<byte> data, int outputLength)
        {
            var builder = new StringBuilder(outputLength);
            var buffer = 0;
            var bitsInBuffer = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsInBuffer += 8;

                while (bitsInBuffer >= 5 && builder.Length < outputLength)
                {
                    var index = (buffer >> (bitsInBuffer - 5)) & 31;
                    builder.Append(CrockfordAlphabet[index]);
                    bitsInBuffer -= 5;
                }
            }

            while (builder.Length < outputLength)
            {
                var index = (buffer << (5 - bitsInBuffer)) & 31;
                builder.Append(CrockfordAlphabet[index]);
                bitsInBuffer = Math.Max(bitsInBuffer - 5, 0);
            }

            return builder.ToString();
        }

        private static string ComputeChecksum(string payload)
        {
            var checksumValue = 0;
            foreach (var c in payload)
            {
                var index = CrockfordAlphabet.IndexOf(c);
                if (index < 0)
                {
                    throw new ArgumentException("Payload contains invalid Crockford character.", nameof(payload));
                }

                checksumValue = (checksumValue * 32 + index) % 1024;
            }

            var first = CrockfordAlphabet[(checksumValue >> 5) & 31];
            var second = CrockfordAlphabet[checksumValue & 31];
            return string.Concat(first, second);
        }
    }
}
