using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Integrations.IdoBooking.Services;

public interface IIdoBookingDefinedAddonScrapingService
{
    Task<List<DefinedAddonEntity>> ScrapeAndPersistAsync(CancellationToken cancellationToken = default);
}

public class IdoBookingDefinedAddonScrapingService : IIdoBookingDefinedAddonScrapingService
{
    private const string BaseUrl = "https://client7953.idosell.com";
    private const string LoginPagePath = "/panel/login";
    private const string AddonsPath = "/panel/addons/";

    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
    private readonly ILogger<IdoBookingDefinedAddonScrapingService> _logger;

    public IdoBookingDefinedAddonScrapingService(
        IConfiguration configuration,
        IDbContextFactory<PostgresBookingDbContext> dbContextFactory,
        ILogger<IdoBookingDefinedAddonScrapingService> logger)
    {
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<List<DefinedAddonEntity>> ScrapeAndPersistAsync(CancellationToken cancellationToken = default)
    {
        var username = _configuration["IDOBOOKING_API_USER"];
        var password = _configuration["IDOBOOKING_API_PWD"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Missing IDOBOOKING_API_USER or IDOBOOKING_API_PWD configuration.");
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };

        await LoginAsync(client, username, password, cancellationToken);

        var addons = await ScrapeAddonsAsync(client, cancellationToken);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.DefinedAddons.RemoveRange(dbContext.DefinedAddons);
        await dbContext.DefinedAddons.AddRangeAsync(addons, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Scraped and stored {Count} defined addons.", addons.Count);

        return addons;
    }

    private async Task LoginAsync(HttpClient client, string username, string password, CancellationToken cancellationToken)
    {
        var loginResponse = await client.GetAsync(LoginPagePath, cancellationToken);
        loginResponse.EnsureSuccessStatusCode();
        var loginHtml = await loginResponse.Content.ReadAsStringAsync(cancellationToken);

        var loginDoc = new HtmlDocument();
        loginDoc.LoadHtml(loginHtml);

        var formNode = loginDoc.DocumentNode.SelectSingleNode("//form")
            ?? throw new InvalidOperationException("Could not find login form on IdoSell panel page.");

        var action = formNode.GetAttributeValue("action", LoginPagePath);
        var postUrl = string.IsNullOrWhiteSpace(action) ? LoginPagePath : action;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in formNode.SelectNodes(".//input") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = input.GetAttributeValue("name", string.Empty);
            var value = input.GetAttributeValue("value", string.Empty);
            var type = input.GetAttributeValue("type", string.Empty);

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.Equals(type, "submit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fields[name] = value;
        }

        SetCredentialField(fields, new[] { "login", "username", "user", "email" }, username);
        SetCredentialField(fields, new[] { "password", "pass", "passwd" }, password);

        using var content = new FormUrlEncodedContent(fields);
        var postResponse = await client.PostAsync(postUrl, content, cancellationToken);
        postResponse.EnsureSuccessStatusCode();

        var addonsResponse = await client.GetAsync(AddonsPath, cancellationToken);
        addonsResponse.EnsureSuccessStatusCode();

        if (addonsResponse.RequestMessage?.RequestUri is Uri finalUri && finalUri.AbsolutePath.Contains("/panel/login", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Login to IdoSell panel failed. Redirected back to login page.");
        }
    }

    private static void SetCredentialField(Dictionary<string, string> fields, IEnumerable<string> aliases, string value)
    {
        foreach (var alias in aliases)
        {
            if (fields.ContainsKey(alias))
            {
                fields[alias] = value;
                return;
            }
        }

        fields[aliases.First()] = value;
    }

    private async Task<List<DefinedAddonEntity>> ScrapeAddonsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var addonsResponse = await client.GetAsync(AddonsPath, cancellationToken);
        addonsResponse.EnsureSuccessStatusCode();
        var addonsHtml = await addonsResponse.Content.ReadAsStringAsync(cancellationToken);

        var listDoc = new HtmlDocument();
        listDoc.LoadHtml(addonsHtml);

        var pageHeaders = listDoc.DocumentNode.SelectNodes("//div[contains(@class,'page-header')]");
        var result = new List<DefinedAddonEntity>();

        if (pageHeaders is null)
        {
            return result;
        }

        foreach (var pageHeader in pageHeaders)
        {
            var addonType = CleanText(pageHeader.SelectSingleNode(".//h2")?.InnerText);
            var table = pageHeader.SelectSingleNode(".//table[contains(@class,'dataTable')]");
            if (table is null)
            {
                continue;
            }

            var rows = table.SelectNodes(".//tbody/tr");
            if (rows is null)
            {
                continue;
            }

            foreach (var row in rows)
            {
                var cells = row.SelectNodes("./td");
                if (cells is null || cells.Count < 3)
                {
                    continue;
                }

                var name = CleanText(cells[0].InnerText);
                var paymentTypeShort = CleanText(cells[1].InnerText);
                var href = cells[2].SelectSingleNode(".//a[@href]")?.GetAttributeValue("href", string.Empty);

                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var idoBookingId = ExtractAddonId(href);
                if (idoBookingId <= 0)
                {
                    continue;
                }

                var details = await ScrapeAddonDetailsAsync(client, href, cancellationToken);

                var definition = new DefinedAddonDefinition
                {
                    Details = new List<LocalizedAddonName>
                    {
                        new()
                        {
                            Lang = "PL",
                            Name = details.NamePl,
                            Description = details.NamePl,
                            PaymentTypeDescription = string.IsNullOrWhiteSpace(details.PaymentTypeDescription) ? details.SelectedAddonTypeValue : details.PaymentTypeDescription,
                            PaymentTypeShortDescription = paymentTypeShort
                        },
                        new()
                        {
                            Lang = "EN",
                            Name = details.NameEn,
                            Description = details.NameEn,
                            PaymentTypeDescription = string.IsNullOrWhiteSpace(details.PaymentTypeDescription) ? details.SelectedAddonTypeValue : details.PaymentTypeDescription,
                            PaymentTypeShortDescription = paymentTypeShort
                        }
                    }
                };

                result.Add(new DefinedAddonEntity
                {
                    IdoBookingId = idoBookingId,
                    Name = name,
                    PaymentType = AddonPaymentType.PayPerPersonPerNight,
                    AddonType = addonType,
                    PriceGross = details.PriceGross,
                    Vat = details.Vat,
                    AddonDefinition = definition
                });
            }
        }

        return result;
    }

    private static int ExtractAddonId(string href)
    {
        var match = Regex.Match(href, @"/id/(?<id>\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["id"].Value, out var id) ? id : 0;
    }

    private async Task<AddonDetails> ScrapeAddonDetailsAsync(HttpClient client, string href, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(href, cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var selectedAddonType = doc.DocumentNode.SelectSingleNode("//select[@id='fg_addon_type_id']/option[@selected]");
        var selectedAddonTypeValue = selectedAddonType?.GetAttributeValue("value", string.Empty) ?? string.Empty;
        var paymentTypeDescription = CleanText(selectedAddonType?.InnerText);

        var namePl = doc.DocumentNode.SelectSingleNode("//input[@id='fg_name_1']")?.GetAttributeValue("value", string.Empty) ?? string.Empty;
        var nameEn = doc.DocumentNode.SelectSingleNode("//input[@id='fg_name_2']")?.GetAttributeValue("value", string.Empty) ?? string.Empty;

        var priceRaw = doc.DocumentNode.SelectSingleNode("//input[@id='fg_prices_1']")?.GetAttributeValue("value", "0") ?? "0";
        var vatRaw = doc.DocumentNode.SelectSingleNode("//input[@id='fg_vat']")?.GetAttributeValue("value", "0") ?? "0";

        return new AddonDetails
        {
            NamePl = namePl,
            NameEn = nameEn,
            PaymentTypeDescription = paymentTypeDescription,
            SelectedAddonTypeValue = selectedAddonTypeValue,
            PriceGross = ParseDecimal(priceRaw),
            Vat = ParseDecimal(vatRaw)
        };
    }

    private static decimal ParseDecimal(string input)
    {
        var normalized = (input ?? string.Empty).Trim().Replace(" ", string.Empty).Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static string CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WebUtility.HtmlDecode(value).Trim();
    }

    private sealed record AddonDetails
    {
        public string NamePl { get; init; } = string.Empty;
        public string NameEn { get; init; } = string.Empty;
        public string PaymentTypeDescription { get; init; } = string.Empty;
        public string SelectedAddonTypeValue { get; init; } = string.Empty;
        public decimal PriceGross { get; init; }
        public decimal Vat { get; init; }
    }
}
