using RentoomBookingWeb.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace RentoomBookingWeb.Services
{
    public class GusService : IGusService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // Adres i klucz testowy GUS
        private const string ServiceUrl = "https://wyszukiwarkaregontest.stat.gov.pl/wsBIR/UslugaBIRzewnPubl.svc";
        private const string TestUserKey = "abcde12345abcde12345";

        public GusService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<GusCompanyData> GetCompanyInfoByNipAsync(string nip)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. ZALOGUJ SIĘ (Pobierz SID)
                string sid = await LoginAsync(client);
                if (string.IsNullOrEmpty(sid)) 
                    throw new Exception("Nie udało się zalogować do GUS (pusty SID).");

                // 2. POBIERZ DANE
                return await SearchByNipAsync(client, sid, nip);
            }
            catch (Exception ex)
            {
                // Przekazujemy błąd wyżej, żeby wyświetlił się w komponencie
                throw new Exception($"Błąd połączenia z GUS: {ex.Message}", ex);
            }
        }

        private async Task<string> LoginAsync(HttpClient client)
        {
            var action = "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/Zaloguj";
            
            var soapEnvelope = $@"
                <soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:ns=""http://CIS/BIR/PUBL/2014/07"">
                   <soap:Header xmlns:wsa=""http://www.w3.org/2005/08/addressing"">
                      <wsa:To>{ServiceUrl}</wsa:To>
                      <wsa:Action>{action}</wsa:Action>
                   </soap:Header>
                   <soap:Body>
                      <ns:Zaloguj>
                         <ns:pKluczUzytkownika>{TestUserKey}</ns:pKluczUzytkownika>
                      </ns:Zaloguj>
                   </soap:Body>
                </soap:Envelope>";

            // Używamy nowej metody pomocniczej
            return await SendSoapRequest(client, soapEnvelope, action, "ZalogujResult");
        }

        private async Task<GusCompanyData> SearchByNipAsync(HttpClient client, string sid, string nip)
        {
            // Czysty NIP
            nip = nip.Replace("-", "").Replace(" ", "").Trim();
            
            var action = "http://CIS/BIR/PUBL/2014/07/IUslugaBIRzewnPubl/DaneSzukajPodmioty";

            var soapEnvelope = $@"
                <soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:ns=""http://CIS/BIR/PUBL/2014/07"" xmlns:dat=""http://CIS/BIR/PUBL/2014/07/DataContract"">
                   <soap:Header xmlns:wsa=""http://www.w3.org/2005/08/addressing"">
                      <wsa:To>{ServiceUrl}</wsa:To>
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

            // Dodaj SID do nagłówka (wymagane po zalogowaniu)
            if (!client.DefaultRequestHeaders.Contains("sid"))
            {
                client.DefaultRequestHeaders.Add("sid", sid);
            }

            // Pobierz wynik jako string XML
            var resultString = await SendSoapRequest(client, soapEnvelope, action, "DaneSzukajPodmiotyResult");

            if (string.IsNullOrEmpty(resultString)) return null;

            // Parsowanie właściwych danych firmy
            var dataXml = XDocument.Parse(resultString);
            var root = dataXml.Root?.Element("dane");

            // Sprawdzenie czy GUS nie zwrócił błędu wewnątrz XML (np. nie znaleziono podmiotu)
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
    
    // Nagłówki wymagane przez GUS
    content.Headers.ContentType = new MediaTypeHeaderValue("application/soap+xml");
    content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("action", $"\"{action}\""));

    var response = await client.PostAsync(ServiceUrl, content);
    var responseString = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new Exception($"GUS zwrócił błąd HTTP {response.StatusCode}: {responseString}");
    }

    // --- POPRAWKA: Czyszczenie odpowiedzi Multipart/MTOM ---
    // Jeśli odpowiedź zaczyna się od --uuid, musimy wyciąć z niej XML
    responseString = ExtractXmlFromMultipart(responseString);

    try
    {
        var xdoc = XDocument.Parse(responseString);
        
        // Szukamy wyniku używając LocalName (ignoruje prefiksy s:, soap:, a: itd.)
        return xdoc.Descendants()
                   .FirstOrDefault(x => x.Name.LocalName == resultTagName)
                   ?.Value;
    }
    catch (Exception ex)
    {
        // Teraz, gdy XML jest wyczyszczony, ten błąd nie powinien wystąpić, 
        // chyba że struktura XML jest uszkodzona.
        throw new Exception($"Błąd parsowania XML po oczyszczeniu. Dane: {responseString.Substring(0, Math.Min(responseString.Length, 200))}...", ex);
    }
}

private string ExtractXmlFromMultipart(string rawResponse)
{
    rawResponse = rawResponse.Trim();

    // Sprawdzamy, czy to odpowiedź "zaśmiecona" nagłówkami MIME (zaczyna się od --uuid)
    if (rawResponse.StartsWith("--uuid:") || rawResponse.Contains("Content-Type: application/xop+xml"))
    {
        // Szukamy początku koperty SOAP. GUS zwraca <s:Envelope> lub <soap:Envelope>
        int startIndex = rawResponse.IndexOf("<s:Envelope");
        if (startIndex == -1) startIndex = rawResponse.IndexOf("<soap:Envelope");

        if (startIndex > -1)
        {
            // Szukamy końca koperty
            int endIndex = rawResponse.IndexOf("</s:Envelope>");
            if (endIndex == -1) endIndex = rawResponse.IndexOf("</soap:Envelope>");

            if (endIndex > -1)
            {
                // Przesuwamy indeks końca, aby objął cały tag zamykający (np. </s:Envelope>)
                // Szukamy znaku '>' zamykającego tag
                endIndex = rawResponse.IndexOf(">", endIndex) + 1;

                // Wycinamy czysty XML
                return rawResponse.Substring(startIndex, endIndex - startIndex);
            }
        }
    }

    // Jeśli to nie jest Multipart, zwracamy oryginał (może to już czysty XML)
    return rawResponse;
}
    }
}