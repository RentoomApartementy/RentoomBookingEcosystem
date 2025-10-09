using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{

    public interface IIdoBookingConnectService
    {
        Task<TResponse?> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest request, CancellationToken cancellationToken);
        AuthenticateType AuthObjectIdo();
    }
    public class IdoBookingConnectService: IIdoBookingConnectService
    {

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IdoBookingConnectService> _logger;
        private readonly string _baseApiUrl;
        private readonly string _systemUser;
        private readonly string _hashedPassword;
        private readonly string _apiLanguage;

        public IdoBookingConnectService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<IdoBookingConnectService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var baseUrl = configuration?["IDOBOOKING_BASE_API_URL"];
            _systemUser = configuration?["IDOBOOKING_API_USER"] ?? throw new InvalidOperationException("IDOBOOKING_API_USER configration is missing. ");
            var systemPassword = configuration?["IDOBOOKING_API_PWD"] ?? throw new InvalidOperationException("IDOBOOKING_API_PWD configration is missing.");

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException("IDOBOOKING_BASE_API_URL configuration is missiing.");
            }

            _baseApiUrl = baseUrl.TrimEnd('/') + "/";
            _hashedPassword = IdoBookingBaseHelper.HashPassword(systemPassword);
            _apiLanguage = configuration?["IDOBOOKING_API_LANG"] ?? "eng";
        }

        public AuthenticateType AuthObjectIdo()
        {
            return new AuthenticateType
            {
                SystemKey = IdoBookingBaseHelper.GenerateKey(_hashedPassword),
                SystemLogin = _systemUser,
                Lang = _apiLanguage
            };
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string relativeUrl, TRequest request, CancellationToken cancellationToken)
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = new Uri(new Uri(_baseApiUrl, UriKind.Absolute), relativeUrl);
            var payload = JsonHelper.SerializeOnlyNonNullProperties(request);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending request to {Url} with payload length {Length} bytes.", url, payload.Length);

            using var response = await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("IdoBooking API request to {Url} failed with status {StatusCode}. Response: {Response}", url, response.StatusCode, responseContent);
                response.EnsureSuccessStatusCode();
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                return default;
            }

            return Newtonsoft.Json.JsonConvert.DeserializeObject<TResponse>(responseContent);
        }


    }

    public static class IdoBookingBaseHelper
    {
        public static string GenerateKey(string hashedPassword)
        {
            System.Security.Cryptography.HashAlgorithm hash = System.Security.Cryptography.SHA1.Create();
            string date = string.Format("{0:yyyyMMdd}", DateTime.Now);
            string strToHash = date + hashedPassword;
            byte[] keyBytes, hashBytes;
            keyBytes = Encoding.UTF8.GetBytes(strToHash);
            hashBytes = hash.ComputeHash(keyBytes);
            string hashedString = string.Empty;
            foreach (byte b in hashBytes)
            {
                hashedString += string.Format("{0:x2}", b);
            }
            Console.WriteLine("pwd " + hashedString);
            return hashedString;
        }

        public static string HashPassword(string password)
        {
            System.Security.Cryptography.HashAlgorithm hash = System.Security.Cryptography.SHA1.Create();
            byte[] keyBytes, hashBytes;
            keyBytes = Encoding.UTF8.GetBytes(password);
            hashBytes = hash.ComputeHash(keyBytes);
            string hashedString = string.Empty;
            foreach (byte b in hashBytes)
            {
                hashedString += string.Format("{0:x2}", b);
            }
            Console.WriteLine("hashed password: " + hashedString);
            return hashedString;
        }

        /*public static AuthenticateType AuthenticateIdbApi()
        {
            return new AuthenticateType
            {
                SystemKey = GenerateKey(_hashedPassword),
                SystemLogin = _systemUser,
                Lang = _apiLanguage
            };
        }*/
    }
}
