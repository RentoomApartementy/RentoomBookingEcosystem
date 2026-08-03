using Microsoft.Extensions.Logging;
using Moq;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Models.RentoomBooking;
using RentoomBooking.SharedClasses.Services;
using RentoomBooking.SharedClasses.Services.IdoBooking;
using RentoomBooking.SharedClasses.Services.Upsell;
using Xunit;

namespace RentoomBooking.SharedClasses.Tests;

public sealed class RentoomOfferServiceFilterTests
{
    [Fact]
    public async Task GetOfferWithFilter_IntersectsApartmentsForAllSelectedAddons()
    {
        var idoOfferService = new Mock<IIdoOfferService>(MockBehavior.Strict);
        var apartmentsService = new Mock<IApartmentsService>(MockBehavior.Strict);
        var upsellCatalogService = new Mock<IUpsellCatalogService>(MockBehavior.Strict);
        ApartmentQueryFilter? capturedFilter = null;

        upsellCatalogService
            .Setup(service => service.GetApartmentIdsForUpsellAsync(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync([1, 2]);
        upsellCatalogService
            .Setup(service => service.GetApartmentIdsForUpsellAsync(200, It.IsAny<CancellationToken>()))
            .ReturnsAsync([2, 3]);
        apartmentsService
            .Setup(service => service.GetApartmentsByFilterAsync(
                It.IsAny<ApartmentQueryFilter>(),
                It.IsAny<CancellationToken>()))
            .Callback<ApartmentQueryFilter, CancellationToken>((filter, _) => capturedFilter = filter)
            .ReturnsAsync([]);

        var service = new RentoomOfferService(
            idoOfferService.Object,
            apartmentsService.Object,
            upsellCatalogService.Object,
            Mock.Of<ILogger<IRentoomOfferService>>());

        var result = await service.getOfferWitFilter(new RentoomQueryOffer
        {
            IdoOfferParams = new PricingOffersRequest(),
            ApartmentFilterParams = new ApartmentFilters
            {
                ApartmentAddonFilter = [100, 200],
                ApartmentAmenitiesFilter = [10, 20, 30]
            }
        });

        Assert.NotNull(result);
        Assert.Empty(result!.ApartmentObjects);
        Assert.NotNull(capturedFilter);
        Assert.Equal([2], capturedFilter!.ApartmentIds);
        Assert.Equal([10, 20, 30], capturedFilter.ApartmentAmenityIds);
        idoOfferService.VerifyNoOtherCalls();
    }
}
