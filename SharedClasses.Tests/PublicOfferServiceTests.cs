using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using Xunit;

namespace SharedClasses.Tests;

public class PublicOfferServiceTests
{
    private static IdoOfferService CreateService(Mock<IIdoBookingConnectService> connectMock)
    {
        return new IdoOfferService(
            connectMock.Object,
            Mock.Of<ILogger<IdoOfferService>>(),
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static Mock<IIdoBookingConnectService> CreateConnectMock()
        => new(MockBehavior.Strict);

    [Fact]
    public void PublicOfferResponse_Deserializes_FirstImageMinimalPriceAndCurrency()
    {
        const string json = """
        {
          "result": {
            "images": [ { "url": "https://cdn/first.jpg" }, { "url": "https://cdn/second.jpg" } ],
            "minimalPrice": 349.50,
            "currency": "PLN"
          },
          "id": "abc"
        }
        """;

        var response = JsonConvert.DeserializeObject<PublicOfferResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("https://cdn/first.jpg", response!.Result?.Images?[0].Url);
        Assert.Equal(349.50m, response.Result?.MinimalPrice);
        Assert.Equal("PLN", response.Result?.Currency);
        Assert.Null(response.Errors);
        Assert.Null(response.Result?.Errors);
    }

    [Fact]
    public void PublicOfferResponse_Deserializes_ResultErrors()
    {
        const string json = """
        { "result": { "errors": { "faultCode": 12, "faultString": "No offer" } } }
        """;

        var response = JsonConvert.DeserializeObject<PublicOfferResponse>(json);

        Assert.NotNull(response!.Result?.Errors);
        Assert.Equal(12, response.Result!.Errors!.FaultCode);
        Assert.Equal("No offer", response.Result.Errors.FaultString);
    }

    [Fact]
    public async Task GetPublicOfferAsync_MapsFirstImageMinimalPriceAndCurrency()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse
            {
                Result = new PublicOfferResult
                {
                    Images = new List<PublicOfferImage> { new() { Url = "https://cdn/a.jpg" }, new() { Url = "https://cdn/b.jpg" } },
                    MinimalPrice = 250m,
                    Currency = "PLN"
                }
            });

        var service = CreateService(connectMock);

        var offer = await service.GetPublicOfferAsync(256);

        Assert.NotNull(offer);
        Assert.Equal(256, offer!.ApartmentId);
        Assert.Equal(250m, offer.MinimalPrice);
        Assert.Equal("PLN", offer.Currency);
        Assert.Equal("https://cdn/a.jpg", offer.ImageUrl);
    }

    [Fact]
    public async Task GetPublicOfferAsync_WithResultErrors_ReturnsNull()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse
            {
                Result = new PublicOfferResult
                {
                    Errors = new RentoomBooking.SharedClasses.Models.GateErrorType { FaultCode = 1, FaultString = "err" }
                }
            });

        var service = CreateService(connectMock);

        Assert.Null(await service.GetPublicOfferAsync(1));
    }

    [Fact]
    public async Task GetPublicOfferAsync_WithRootErrors_ReturnsNull()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse
            {
                Errors = new RentoomBooking.SharedClasses.Models.GateErrorType { FaultCode = 5, FaultString = "root" }
            });

        var service = CreateService(connectMock);

        Assert.Null(await service.GetPublicOfferAsync(1));
    }

    [Fact]
    public async Task GetPublicOfferAsync_WithoutMinimalPrice_ReturnsNull()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse { Result = new PublicOfferResult { Currency = "PLN" } });

        var service = CreateService(connectMock);

        Assert.Null(await service.GetPublicOfferAsync(1));
    }

    [Fact]
    public async Task GetPublicOfferAsync_CachesSuccessfulResponse()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse { Result = new PublicOfferResult { MinimalPrice = 100m, Currency = "PLN" } });

        var service = CreateService(connectMock);

        var first = await service.GetPublicOfferAsync(42);
        var second = await service.GetPublicOfferAsync(42);

        Assert.NotNull(first);
        Assert.NotNull(second);
        connectMock.Verify(
            x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.IsAny<PublicOfferRequest?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPublicOffersAsync_OneFailingId_DoesNotBlockOthers()
    {
        var connectMock = CreateConnectMock();
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.Is<PublicOfferRequest?>(r => r != null && r.OfferId == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicOfferResponse { Result = new PublicOfferResult { MinimalPrice = 150m, Currency = "PLN" } });
        connectMock
            .Setup(x => x.PostAsync<PublicOfferRequest, PublicOfferResponse>(
                It.IsAny<string>(), It.Is<PublicOfferRequest?>(r => r != null && r.OfferId == 2), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var service = CreateService(connectMock);

        var offers = await service.GetPublicOffersAsync(new[] { 1, 2 });

        Assert.True(offers.ContainsKey(1));
        Assert.False(offers.ContainsKey(2));
        Assert.Equal(150m, offers[1].MinimalPrice);
    }

    [Fact]
    public async Task GetPublicOffersAsync_EmptyInput_ReturnsEmpty()
    {
        var connectMock = CreateConnectMock();
        var service = CreateService(connectMock);

        var offers = await service.GetPublicOffersAsync(Array.Empty<int>());

        Assert.Empty(offers);
        connectMock.VerifyNoOtherCalls();
    }
}
