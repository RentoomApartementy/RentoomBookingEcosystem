using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using Xunit;

namespace RentoomBooking.SharedClasses.Tests;

public sealed class ApartmentRepositoryFilterTests
{
    [Fact]
    public async Task GetApartmentsByFilterAsync_RequiresAllSelectedAmenities()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repository = CreateRepository(options, cache);

        var result = await repository.GetApartmentsByFilterAsync(new ApartmentQueryFilter
        {
            ApartmentAmenityIds = [10, 20, 30]
        });

        Assert.Equal([1, 3], result!.Select(apartment => apartment.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetApartmentsByFilterAsync_CombinesFilterGroupsWithAnd()
    {
        var options = CreateOptions();
        await SeedAsync(options);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var repository = CreateRepository(options, cache);

        var result = await repository.GetApartmentsByFilterAsync(new ApartmentQueryFilter
        {
            ApartmentIds = [1, 2],
            ApartmentAmenityIds = [10, 20, 30],
            ApartmentObjectLocalizationItemRegionNames = ["Centrum"]
        });

        Assert.Equal(1, Assert.Single(result!).Id);
    }

    private static DbContextOptions<PostgresBookingDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<PostgresBookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

    private static ApartmentRepository CreateRepository(
        DbContextOptions<PostgresBookingDbContext> options,
        IMemoryCache cache)
    {
        var factory = new TestDbContextFactory(options);
        var database = new PostgresBookingDatabase(
            factory,
            NullLogger<PostgresBookingDatabase>.Instance,
            cache);

        return new ApartmentRepository(
            database,
            factory,
            Mock.Of<IConfiguration>(),
            NullLogger<ApartmentRepository>.Instance,
            cache);
    }

    private static async Task SeedAsync(DbContextOptions<PostgresBookingDbContext> options)
    {
        await using var context = new PostgresBookingDbContext(options);

        var apartments = new[]
        {
            CreateApartment(1, "Centrum"),
            CreateApartment(2, "Centrum"),
            CreateApartment(3, "Stare Miasto")
        };

        context.ApartmentInfos.AddRange(apartments.Select(apartment => new ApartmentInfoEntity
        {
            Id = apartment.Id,
            Payload = JsonConvert.SerializeObject(apartment)
        }));

        context.ApartmentAmenities.AddRange(
            CreateAmenities(1, 10, 20, 30),
            CreateAmenities(2, 10, 20),
            CreateAmenities(3, 10, 20, 30));

        context.ApartmentMediaAssets.AddRange(apartments.SelectMany(apartment =>
            Enumerable.Range(1, 7).Select(sequence => new ApartmentMediaAssetEntity
            {
                ApartmentId = apartment.Id,
                IdoSourceUrl = $"https://example.test/{apartment.Id}/{sequence}.jpg",
                StorageKey = $"{apartment.Id}/{sequence}.jpg",
                PictureDisplaySequence = sequence
            })));

        await context.SaveChangesAsync();
    }

    private static ApartmentObject CreateApartment(int id, string region) => new()
    {
        Id = id,
        Name = $"Apartment {id}",
        ObjectLocation = new ObjectLocation
        {
            LocalizationItem = new LocalizationItem { Region = region }
        }
    };

    private static ApartmentAmenityEntity CreateAmenities(int apartmentId, params int[] amenityIds) => new()
    {
        Id = apartmentId,
        Payload = JsonConvert.SerializeObject(new ApartmentAmenitiesDocument
        {
            Id = apartmentId,
            ApartmentId = apartmentId,
            Amenities = amenityIds.Select(id => new ObjectAmenity { Id = id }).ToList()
        })
    };

    private sealed class TestDbContextFactory(DbContextOptions<PostgresBookingDbContext> options)
        : IDbContextFactory<PostgresBookingDbContext>
    {
        public PostgresBookingDbContext CreateDbContext() => new(options);

        public Task<PostgresBookingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PostgresBookingDbContext(options));
    }
}
