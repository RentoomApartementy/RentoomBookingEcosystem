using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.ApartmentMedia;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Services.ApartmentMedia
{
    public interface IApartmentMediaCatalogService
    {
        Task<List<ObjectMedium>> GetApartmentMediaAsync(int apartmentId, CancellationToken cancellationToken = default);
        Task<List<ApartmentMediaAssetEntity>> GetAssetEntitiesAsync(int apartmentId, CancellationToken cancellationToken = default);
        Task UpsertAssetsAsync(
            int apartmentId,
            IReadOnlyCollection<ApartmentMediaSyncSourceState> sourceStates,
            ApartmentMediaSyncRunSummary summary,
            CancellationToken cancellationToken = default);
        Task SaveRunSummaryAsync(ApartmentMediaSyncRunSummary summary, CancellationToken cancellationToken = default);
    }

    public sealed class ApartmentMediaCatalogService : IApartmentMediaCatalogService
    {
        private readonly IDbContextFactory<PostgresBookingDbContext> _dbContextFactory;
        private readonly IApartmentPhotoBlobStorage _blobStorage;
        private readonly ILogger<ApartmentMediaCatalogService> _logger;

        public ApartmentMediaCatalogService(
            IDbContextFactory<PostgresBookingDbContext> dbContextFactory,
            IApartmentPhotoBlobStorage blobStorage,
            ILogger<ApartmentMediaCatalogService> logger)
        {
            _dbContextFactory = dbContextFactory;
            _blobStorage = blobStorage;
            _logger = logger;
        }

        public async Task<List<ObjectMedium>> GetApartmentMediaAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entities = await context.ApartmentMediaAssets
                .AsNoTracking()
                .Where(asset => asset.ApartmentId == apartmentId)
                .OrderBy(asset => asset.PictureDisplaySequence)
                .ThenBy(asset => asset.Id)
                .ToListAsync(cancellationToken);

            return entities.Select(asset => new ObjectMedium
            {
                Id = asset.IdoObjectMediaId ?? asset.Id,
                ObjectId = asset.ApartmentId,
                Url = _blobStorage.BuildBlobUrl(asset.StorageKey),
                Extension = asset.Extension,
                Position = asset.PictureDisplaySequence,
                Type = asset.ContentType
            }).ToList();
        }

        public async Task<List<ApartmentMediaAssetEntity>> GetAssetEntitiesAsync(int apartmentId, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await context.ApartmentMediaAssets
                .Where(asset => asset.ApartmentId == apartmentId)
                .OrderBy(asset => asset.PictureDisplaySequence)
                .ThenBy(asset => asset.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task UpsertAssetsAsync(
            int apartmentId,
            IReadOnlyCollection<ApartmentMediaSyncSourceState> sourceStates,
            ApartmentMediaSyncRunSummary summary,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existingAssets = await context.ApartmentMediaAssets
                .Where(asset => asset.ApartmentId == apartmentId)
                .ToListAsync(cancellationToken);

            var sourceMap = sourceStates.ToDictionary(state => state.SourceMedium.Url ?? string.Empty, StringComparer.Ordinal);
            var utcNow = DateTime.UtcNow;

            foreach (var asset in existingAssets)
            {
                if (!sourceMap.ContainsKey(asset.IdoSourceUrl))
                {
                    await _blobStorage.DeleteIfExistsAsync(asset.StorageKey, cancellationToken);
                    context.ApartmentMediaAssets.Remove(asset);
                    summary.DeletedCount++;
                    summary.Changes.Add(new ApartmentMediaSyncChange
                    {
                        ApartmentId = apartmentId,
                        IdoSourceUrl = asset.IdoSourceUrl,
                        StorageKey = asset.StorageKey,
                        Action = "deleted",
                        OldSequence = asset.PictureDisplaySequence
                    });
                }
            }

            foreach (var sourceState in sourceStates)
            {
                var sourceUrl = sourceState.SourceMedium.Url ?? string.Empty;
                if (string.IsNullOrWhiteSpace(sourceUrl))
                {
                    continue;
                }

                var existing = existingAssets.FirstOrDefault(asset => asset.IdoSourceUrl == sourceUrl);
                if (existing is null)
                {
                    context.ApartmentMediaAssets.Add(new ApartmentMediaAssetEntity
                    {
                        ApartmentId = apartmentId,
                        IdoObjectMediaId = sourceState.SourceMedium.Id,
                        IdoSourceUrl = sourceUrl,
                        StorageKey = _blobStorage.BuildStorageKey(apartmentId, sourceUrl, sourceState.SourceMedium.Extension),
                        ContentType = sourceState.ContentType,
                        Extension = sourceState.SourceMedium.Extension,
                        PictureDisplaySequence = sourceState.PictureDisplaySequence,
                        SourceEtag = sourceState.SourceEtag,
                        SourceLastModifiedUtc = sourceState.SourceLastModifiedUtc,
                        SourceContentLength = sourceState.SourceContentLength,
                        ChecksumSha256 = sourceState.ChecksumSha256,
                        CreatedAt = utcNow,
                        UpdatedAt = utcNow
                    });

                    continue;
                }

                existing.IdoObjectMediaId = sourceState.SourceMedium.Id;
                existing.ContentType = sourceState.ContentType;
                existing.Extension = sourceState.SourceMedium.Extension;
                existing.SourceEtag = sourceState.SourceEtag;
                existing.SourceLastModifiedUtc = sourceState.SourceLastModifiedUtc;
                existing.SourceContentLength = sourceState.SourceContentLength;
                existing.ChecksumSha256 = sourceState.ChecksumSha256;

                if (existing.PictureDisplaySequence != sourceState.PictureDisplaySequence)
                {
                    summary.SequenceUpdatedCount++;
                    summary.Changes.Add(new ApartmentMediaSyncChange
                    {
                        ApartmentId = apartmentId,
                        IdoSourceUrl = sourceUrl,
                        StorageKey = existing.StorageKey,
                        Action = "sequence_updated",
                        OldSequence = existing.PictureDisplaySequence,
                        NewSequence = sourceState.PictureDisplaySequence
                    });
                }

                existing.PictureDisplaySequence = sourceState.PictureDisplaySequence;
                existing.UpdatedAt = utcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveRunSummaryAsync(ApartmentMediaSyncRunSummary summary, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await context.ApartmentMediaSyncRuns.FirstOrDefaultAsync(run => run.RunId == summary.RunId, cancellationToken);

            if (existing is null)
            {
                context.ApartmentMediaSyncRuns.Add(MapRun(summary));
            }
            else
            {
                existing.StartedAt = summary.StartedAt;
                existing.FinishedAt = summary.FinishedAt;
                existing.Status = summary.Status;
                existing.ApartmentsProcessed = summary.ApartmentsProcessed;
                existing.MediaItemsSeen = summary.MediaItemsSeen;
                existing.DownloadedCount = summary.DownloadedCount;
                existing.ReplacedCount = summary.ReplacedCount;
                existing.DeletedCount = summary.DeletedCount;
                existing.SequenceUpdatedCount = summary.SequenceUpdatedCount;
                existing.FailedCount = summary.FailedCount;
                existing.SummaryJson = JsonConvert.SerializeObject(summary.Changes);
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Saved apartment media sync run {RunId}. Status={Status}, ApartmentsProcessed={ApartmentsProcessed}, Downloaded={Downloaded}, Replaced={Replaced}, Deleted={Deleted}, SequenceUpdated={SequenceUpdated}, Failed={Failed}.",
                summary.RunId,
                summary.Status,
                summary.ApartmentsProcessed,
                summary.DownloadedCount,
                summary.ReplacedCount,
                summary.DeletedCount,
                summary.SequenceUpdatedCount,
                summary.FailedCount);
        }

        private static ApartmentMediaSyncRunEntity MapRun(ApartmentMediaSyncRunSummary summary)
        {
            return new ApartmentMediaSyncRunEntity
            {
                RunId = summary.RunId,
                StartedAt = summary.StartedAt,
                FinishedAt = summary.FinishedAt,
                Status = summary.Status,
                ApartmentsProcessed = summary.ApartmentsProcessed,
                MediaItemsSeen = summary.MediaItemsSeen,
                DownloadedCount = summary.DownloadedCount,
                ReplacedCount = summary.ReplacedCount,
                DeletedCount = summary.DeletedCount,
                SequenceUpdatedCount = summary.SequenceUpdatedCount,
                FailedCount = summary.FailedCount,
                SummaryJson = JsonConvert.SerializeObject(summary.Changes)
            };
        }
    }
}
