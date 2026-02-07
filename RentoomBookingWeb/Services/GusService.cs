using RentoomBookingWeb.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace RentoomBookingWeb.Services
{
    public class GusService : IGusService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _memoryCache; // Do trzymania SID sesji
        private readonly string _serviceUrl;
        private readonly string _userKey;

        // Statyczny semafor: blokuje jednoczesne logowanie wielu wątków. 
        // Dzięki temu, jak 10 osób kliknie naraz, zalogujemy się tylko raz.
        private static readonly SemaphoreSlim _loginSemaphore = new(1, 1);

        public GusService(
            IHttpClientFactory httpClientFactory, 
            IOptions<GusApiSettings> settings,
            IMemoryCache memoryCache)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _serviceUrl = settings.Value.ServiceUrl;
            _userKey = settings.Value.UserKey;
        }

        public async Task<GusCompanyData> GetCompanyInfoByNipAsync(string nip)
        {
            // 1. Walidacja wstępna (oszczędzamy requesty do GUS)
            var cleanNip = ValidateAndCleanNip(nip);

            // 2. Pobieramy SID (z Cache lub logujemy się jeśli wygasł)
            string sid = await GetCachedSidAsync();

            try
            {
                var client = _httpClientFactory.CreateClient();
                
                // 3. Pobieramy dane właściwe
                return await SearchByNipAsync(client, sid, cleanNip);
            }
            catch (Exception ex)
            {
                // Logowanie błędu (tutaj warto dodać ILogger)
                throw new Exception($"Nie udało się pobrać danych z GUS: {ex.Message}", ex);
            }
        }

        // --- Logika Zarządzania Sesją (Nowość) ---

        private async Task<string> GetCachedSidAsync()
        {
            const string cacheKey = "GUS_SESSION_SID";

            // Sprawdzamy czy mamy aktywną sesję w pamięci
            if (_memoryCache.TryGetValue(cacheKey, out string cachedSid))
            {
                return cachedSid;
            }

            // Jeśli nie ma sesji, blokujemy wątki, żeby tylko jeden wykonał logowanie
            await _loginSemaphore.WaitAsync();
            try
            {
                // Sprawdzamy jeszcze raz po wejściu do strefy chronionej (double-check locking)
                if (_memoryCache.TryGetValue(cacheKey, out cachedSid))
                {
                    return cachedSid;
                }

                // Faktyczne logowanie do GUS
                var client = _httpClientFactory.CreateClient();
                string newSid = await LoginAsync(client);

                if (string.IsNullOrEmpty(newSid))
                    throw new Exception("Logowanie do GUS zwróciło pusty identyfikator sesji.");

                // Zapisujemy SID w cache na 45 minut (sesja GUS trwa zazwyczaj 60 min)
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(45));

                _memoryCache.Set(cacheKey, newSid, cacheOptions);

                return newSid;
            }
            finally
            {
                _loginSemaphore.Release();
            }
        }

        private string ValidateAndCleanNip(string nip)
        {
            if (string.IsNullOrWhiteSpace(nip)) throw new ArgumentException("NIP nie może być pusty.");
            
            // Usuwamy myślniki i spacje
            var clean = nip.Replace("-", "").Replace(" ", "").Trim();

            // Sprawdzamy czy to same cyfry i czy jest ich 10 (Regex jest tu szybki i bezpieczny)
            if (!Regex.IsMatch(clean, @"^\d{10}$"))
            {
                throw new ArgumentException("Nieprawidłowy format NIP (wymagane 10 cyfr).");
            }

            return clean;
        }

        // --- Metody Oryginalne (z drobnymi poprawkami bezpieczeństwa) ---

        private async Task<string> LoginAsync(HttpClient client)
        {
            var action = "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/Zaloguj";
            
            var soapEnvelope = $@"
                <soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:ns=""http://CIS/BIR/PUBL/2014/07"">
                   <soap:Header xmlns:wsa=""http://www.w3.org/2005/08/addressing"">
                      <wsa:To>{_serviceUrl}</wsa:To>
                      <wsa:Action>{action}</wsa:Action>
                   </soap:Header>
                   <soap:Body>
                      <ns:Zaloguj>
                         <ns:pKluczUzytkownika>{_userKey}</ns:pKluczUzytkownika>
                      </ns:Zaloguj>
                   </soap:Body>
                </soap:Envelope>";

            return await SendSoapRequest(client, soapEnvelope, action, "ZalogujResult");
        }

        private async Task<GusCompanyData> SearchByNipAsync(HttpClient client, string sid, string nip)
        {
            var action = "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/DaneSzukajPodmioty";

            // Użycie SecurityElement.Escape nie jest konieczne dla cyfrowego NIP, 
            // ale dobrą praktyką jest nie wstawianie zmiennych bezpośrednio do XML.
            // Tutaj nip jest już po walidacji (same cyfry), więc jest bezpieczny.
            
            var soapEnvelope = $@"
                <soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:ns=""http://CIS/BIR/PUBL/2014/07"" xmlns:dat=""http://CIS/BIR/PUBL/2014/07/DataContract"">
                   <soap:Header xmlns:wsa=""http://www.w3.org/2005/08/addressing"">
                      <wsa:To>{_serviceUrl}</wsa:To>
                      <wsa:Action>{action}</wsa:Action>
                   </soap:Header>
                   <soap:Body>
                      <ns:DaneSzukajPodmioty>
                         <ns:pParametryWyszukiwania>
                            <dat:Nip>{nip}</dat:Nip>
                         </ns:pParametryWyszukiwania>
                      </ns:DaneSzukajPodmioty>
                   </soap:Body>
                </soap:Envelope>";

            if (!client.DefaultRequestHeaders.Contains("sid"))
            {
                client.DefaultRequestHeaders.Add("sid", sid);
            }

            var resultString = await SendSoapRequest(client, soapEnvelope, action, "DaneSzukajPodmiotyResult");

            if (string.IsNullOrEmpty(resultString)) return null;

            var dataXml = XDocument.Parse(resultString);
            var root = dataXml.Root?.Element("dane");

            if (root == null || root.Element("ErrorCode") != null) return null;

            return new GusCompanyData
            {
                Nazwa = root.Element("Nazwa")?.Value,
                Ulica = root.Element("Ulica")?.Value,
                NrNieruchomosci = root.Element("NrNieruchomosci")?.Value,
                NrLokalu = root.Element("NrLokalu")?.Value,
                KodPocztowy = root.Element("KodPocztowy")?.Value,
                Miejscowosc = root.Element("Miejscowosc")?.Value,
                Wojewodztwo = root.Element("Wojewodztwo")?.Value
            };
        }

        private async Task<string> SendSoapRequest(HttpClient client, string envelope, string action, string resultTagName)
        {
            var content = new StringContent(envelope, Encoding.UTF8, "application/soap+xml");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml");
            content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", $"\"{action}\""));

            var response = await client.PostAsync(_serviceUrl, content);
            
            // Jeśli sesja wygasła (GUS zwróci błąd sesji), warto tutaj obsłużyć ponowne logowanie (retry logic),
            // ale w podstawowej wersji po prostu rzucamy wyjątek - cache wygaśnie po 45 min i naprawi się samo.
            
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Tutaj można dodać logowanie responseString dla admina
                throw new Exception($"Błąd komunikacji z GUS (HTTP {response.StatusCode})");
            }

            responseString = ExtractXmlFromMultipart(responseString);

            try
            {
                var xdoc = XDocument.Parse(responseString);
                return xdoc.Descendants()
                           .FirstOrDefault(x => x.Name.LocalName == resultTagName)
                           ?.Value;
            }
            catch (Exception ex)
            {
                 throw new Exception("Błąd parsowania odpowiedzi XML z GUS.", ex);
            }
        }

        private string ExtractXmlFromMultipart(string rawResponse)
        {
            // Ta metoda była OK, zostawiłem ją bez zmian, 
            // ewentualnie dodałem trimowanie dla pewności.
            rawResponse = rawResponse.Trim();

            if (rawResponse.StartsWith("--uuid:") || rawResponse.Contains("Content-Type: application/xop+xml"))
            {
                int startIndex = rawResponse.IndexOf("<s:Envelope");
                if (startIndex == -1) startIndex = rawResponse.IndexOf("<soap:Envelope");

                if (startIndex > -1)
                {
                    int endIndex = rawResponse.IndexOf("</s:Envelope>");
                    if (endIndex == -1) endIndex = rawResponse.IndexOf("</soap:Envelope>");

                    if (endIndex > -1)
                    {
                        endIndex = rawResponse.IndexOf(">", endIndex) + 1;
                        return rawResponse.Substring(startIndex, endIndex - startIndex);
                    }
                }
            }
            return rawResponse;
        }
    }
}