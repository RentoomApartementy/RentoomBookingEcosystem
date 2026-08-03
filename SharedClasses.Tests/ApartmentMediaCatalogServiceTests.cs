using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.ApartmentMedia;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using Xunit;

namespace RentoomBooking.SharedClasses.Tests;

public sealed class ApartmentMediaCatalogServiceTests
{
    [Fact]
    public async Task UpsertAssetsAsync_RemovesExistingDuplicateAndItsBlobs()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<PostgresBookingDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await SeedAsync(options);
        var storage = new FakeApartmentPhotoBlobStorage();
        var service = new ApartmentMediaCatalogService(
            new TestDbContextFactory(options),
            storage,
            NullLogger<ApartmentMediaCatalogService>.Instance);
        var summary = new ApartmentMediaSyncRunSummary();

        await service.UpsertAssetsAsync(
            10,
            [SourceState("https://ido.example/first.jpg", 1, "checksum")],
            summary,
            new Dictionary<string, ApartmentMediaDuplicateSource>
            {
                ["https://ido.example/duplicate.jpg"] = new()
                {
                    ChecksumSha256 = "checksum",
                    RetainedIdoSourceUrl = "https://ido.example/first.jpg",
                    RetainedSequence = 1,
                    DuplicateSequence = 2
                }
            });

        await using var verificationContext = new PostgresBookingDbContext(options);
        var remaining = await verificationContext.ApartmentMediaAssets.OrderBy(asset => asset.Id).ToListAsync();
        Assert.Single(remaining);
        Assert.Equal("https://ido.example/first.jpg", remaining[0].IdoSourceUrl);
        Assert.Contains("duplicate-original.jpg", storage.DeletedKeys);
        Assert.Contains("duplicate-card.webp", storage.DeletedKeys);
        Assert.DoesNotContain("first-original.jpg", storage.DeletedKeys);
        Assert.Equal(1, summary.DeletedCount);
        Assert.Contains(summary.Changes, change => change.Reason == "duplicate_checksum" && change.Variant == "original");
        Assert.Contains(summary.Changes, change => change.Reason == "duplicate_checksum" && change.Variant == "card");
    }

    [Fact]
    public async Task UpsertAssetsAsync_KeepsDatabaseRecordWhenBlobDeletionFails()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<PostgresBookingDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await SeedAsync(options);
        var storage = new FakeApartmentPhotoBlobStorage { ThrowOnDeleteKey = "duplicate-card.webp" };
        var service = new ApartmentMediaCatalogService(
            new TestDbContextFactory(options),
            storage,
            NullLogger<ApartmentMediaCatalogService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpsertAssetsAsync(
            10,
            [SourceState("https://ido.example/first.jpg", 1, "checksum")],
            new ApartmentMediaSyncRunSummary(),
            new Dictionary<string, ApartmentMediaDuplicateSource>
            {
                ["https://ido.example/duplicate.jpg"] = new()
                {
                    ChecksumSha256 = "checksum",
                    RetainedIdoSourceUrl = "https://ido.example/first.jpg"
                }
            }));

        await using var verificationContext = new PostgresBookingDbContext(options);
        Assert.Equal(2, await verificationContext.ApartmentMediaAssets.CountAsync());
    }

    [Fact]
    public async Task UpsertAssetsAsync_DoesNotDeleteBlobReferencedByKeptAsset()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<PostgresBookingDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await SeedAsync(options, sharedOriginalKey: true);
        var storage = new FakeApartmentPhotoBlobStorage();
        var service = new ApartmentMediaCatalogService(
            new TestDbContextFactory(options),
            storage,
            NullLogger<ApartmentMediaCatalogService>.Instance);

        await service.UpsertAssetsAsync(
            10,
            [SourceState("https://ido.example/first.jpg", 1, "checksum")],
            new ApartmentMediaSyncRunSummary(),
            new Dictionary<string, ApartmentMediaDuplicateSource>
            {
                ["https://ido.example/duplicate.jpg"] = new()
                {
                    ChecksumSha256 = "checksum",
                    RetainedIdoSourceUrl = "https://ido.example/first.jpg"
                }
            });

        Assert.DoesNotContain("first-original.jpg", storage.DeletedKeys);
        Assert.Contains("duplicate-card.webp", storage.DeletedKeys);
    }

    private static async Task SeedAsync(DbContextOptions<PostgresBookingDbContext> options, bool sharedOriginalKey = false)
    {
        await using var context = new PostgresBookingDbContext(options);
        context.ApartmentMediaAssets.AddRange(
            new ApartmentMediaAssetEntity
            {
                Id = 1,
                ApartmentId = 10,
                IdoSourceUrl = "https://ido.example/first.jpg",
                StorageKey = "first-original.jpg",
                CardStorageKey = "first-card.webp",
                PictureDisplaySequence = 1,
                ChecksumSha256 = "checksum"
            },
            new ApartmentMediaAssetEntity
            {
                Id = 2,
                ApartmentId = 10,
                IdoSourceUrl = "https://ido.example/duplicate.jpg",
                StorageKey = sharedOriginalKey ? "first-original.jpg" : "duplicate-original.jpg",
                CardStorageKey = "duplicate-card.webp",
                PictureDisplaySequence = 2,
                ChecksumSha256 = "checksum"
            });
        await context.SaveChangesAsync();
    }

    private static ApartmentMediaSyncSourceState SourceState(string url, int sequence, string checksum) => new()
    {
        SourceMedium = new ObjectMedium { Url = url, Extension = "jpg" },
        PictureDisplaySequence = sequence,
        ChecksumSha256 = checksum
    };

    private sealed class TestDbContextFactory(DbContextOptions<PostgresBookingDbContext> options) : IDbContextFactory<PostgresBookingDbContext>
    {
        public PostgresBookingDbContext CreateDbContext() => new(options);

        public Task<PostgresBookingDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PostgresBookingDbContext(options));
    }

    private sealed class FakeApartmentPhotoBlobStorage : IApartmentPhotoBlobStorage
    {
        public HashSet<string> DeletedKeys { get; } = new(StringComparer.Ordinal);
        public string? ThrowOnDeleteKey { get; init; }

        public Task UploadAsync(string storageKey, Stream content, string? contentType, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            if (string.Equals(storageKey, ThrowOnDeleteKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Blob deletion failed.");
            }

            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<(Stream Content, string? ContentType)> DownloadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<(Stream, string?)>((new MemoryStream(), null));

        public string BuildBlobUrl(string storageKey) => storageKey;
        public string BuildStorageKey(int apartmentId, string sourceUrl, string? extension) => sourceUrl;
        public string BuildVariantStorageKey(int apartmentId, string sourceUrl, string variantName, string? extension) => sourceUrl;
    }
}
