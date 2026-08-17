using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Models;
using RentoomBooking.SharedClasses.Integrations.Bitrix.Services;
using Xunit;

namespace SharedClasses.Tests;

public sealed class BitrixContactServiceTests
{
    [Fact]
    public async Task UpsertWithoutEmail_FindsByReservationIdThenCreatesWithoutEmailField()
    {
        var requests = new List<(string Path, string Body)>();
        var handler = new RecordingHandler(async request =>
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            requests.Add((request.RequestUri!.AbsolutePath, body));

            var responseJson = request.RequestUri.AbsolutePath.EndsWith("crm.contact.list.json", StringComparison.Ordinal)
                ? "{\"result\":[]}" 
                : "{\"result\":915}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var service = CreateService(handler);

        var contactId = await service.UpsertContactByEmailAsync(new CreateContactRequest
        {
            FirstName = "Jan",
            LastName = "Kowalski",
            Email = string.Empty,
            Phone = "+48123123123",
            ReservationId = 1234
        });

        Assert.Equal(915, contactId);
        Assert.Equal(2, requests.Count);

        using var lookupPayload = JsonDocument.Parse(requests[0].Body);
        Assert.Equal(
            1234,
            lookupPayload.RootElement
                .GetProperty("filter")
                .GetProperty(BitrixService.ContactIdoReservationIdFieldName)
                .GetInt32());

        using var createPayload = JsonDocument.Parse(requests[1].Body);
        var fields = createPayload.RootElement.GetProperty("fields");
        Assert.False(fields.TryGetProperty("EMAIL", out _));
        Assert.Equal(1234, fields.GetProperty(BitrixService.ContactIdoReservationIdFieldName).GetInt32());
    }

    private static BitrixService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bitrix:Domain"] = "https://example.test/rest",
                ["Bitrix:UserIdForWebhook"] = "1",
                ["Bitrix:WebhookId"] = "secret"
            })
            .Build();

        return new BitrixService(new HttpClient(handler), configuration);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return responseFactory(request);
        }
    }
}
