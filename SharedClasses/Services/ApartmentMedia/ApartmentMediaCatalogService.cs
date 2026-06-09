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
        Task<IReadOnlyDictionary<int, List<ObjectMedium>>> GetApartmentMediaBatchAsync(
            IReadOnlyCollection<int> apartmentIds,
            CancellationToken cancellationToken = default);
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

            return entities.Select(MapAssetToObjectMedium).ToList();
        }

        public async Task<IReadOnlyDictionary<int, List<ObjectMedium>>> GetApartmentMediaBatchAsync(
            IReadOnlyCollection<int> apartmentIds,
            CancellationToken cancellationToken = default)
        {
            var requestedApartmentIds = apartmentIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (requestedApartmentIds.Count == 0)
            {
                return new Dictionary<int, List<ObjectMedium>>();
            }

            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entities = await context.ApartmentMediaAssets
                .AsNoTracking()
                .Where(asset => requestedApartmentIds.Contains(asset.ApartmentId))
                .OrderBy(asset => asset.ApartmentId)
                .ThenBy(asset => asset.PictureDisplaySequence)
                .ThenBy(asset => asset.Id)
                .ToListAsync(cancellationToken);

            var mediaByApartmentId = requestedApartmentIds.ToDictionary(
                apartmentId => apartmentId,
                _ => new List<ObjectMedium>());

            foreach (var entity in entities)
            {
                mediaByApartmentId[entity.ApartmentId].Add(MapAssetToObjectMedium(entity));
            }

            return mediaByApartmentId;
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
                    if (!string.IsNullOrWhiteSpace(asset.CardStorageKey))
                    {
                        await _blobStorage.DeleteIfExistsAsync(asset.CardStorageKey, cancellationToken);
                        summary.Changes.Add(new ApartmentMediaSyncChange
                        {
                            ApartmentId = apartmentId,
                            IdoSourceUrl = asset.IdoSourceUrl,
                            StorageKey = asset.CardStorageKey,
                            Action = "deleted",
                            Variant = "card",
                            Reason = "source_removed",
                            OldSequence = asset.PictureDisplaySequence,
                            ContentType = asset.CardContentType
                        });

                        _logger.LogInformation(
                            "Apartment media sync item processed. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, IdoSourceUrl={IdoSourceUrl}, StorageKey={StorageKey}, Variant={Variant}, Action={Action}, Reason={Reason}, OldSequence={OldSequence}, NewSequence={NewSequence}, ContentType={ContentType}",
                            summary.RunId,
                            apartmentId,
                            asset.IdoSourceUrl,
                            asset.CardStorageKey,
                            "card",
                            "deleted",
                            "source_removed",
                            asset.PictureDisplaySequence,
                            null,
                            asset.CardContentType);
                    }

                    context.ApartmentMediaAssets.Remove(asset);
                    summary.DeletedCount++;
                    summary.Changes.Add(new ApartmentMediaSyncChange
                    {
                        ApartmentId = apartmentId,
                        IdoSourceUrl = asset.IdoSourceUrl,
                        StorageKey = asset.StorageKey,
                        Action = "deleted",
                        Variant = "original",
                        Reason = "source_removed",
                        OldSequence = asset.PictureDisplaySequence,
                        ContentType = asset.ContentType
                    });

                    _logger.LogInformation(
                        "Apartment media sync item processed. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, IdoSourceUrl={IdoSourceUrl}, StorageKey={StorageKey}, Variant={Variant}, Action={Action}, Reason={Reason}, OldSequence={OldSequence}, NewSequence={NewSequence}, ContentType={ContentType}",
                        summary.RunId,
                        apartmentId,
                        asset.IdoSourceUrl,
                        asset.StorageKey,
                        "original",
                        "deleted",
                        "source_removed",
                        asset.PictureDisplaySequence,
                        null,
                        asset.ContentType);
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
                        CardStorageKey = sourceState.CardStorageKey,
                        CardContentType = sourceState.CardContentType,
                        CardWidth = sourceState.CardWidth,
                        CardHeight = sourceState.CardHeight,
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
                existing.ContentType = sourceState.ContentType ?? existing.ContentType;
                existing.Extension = sourceState.SourceMedium.Extension;
                existing.CardStorageKey = sourceState.CardStorageKey ?? existing.CardStorageKey;
                existing.CardContentType = sourceState.CardContentType ?? existing.CardContentType;
                existing.CardWidth = sourceState.CardWidth ?? existing.CardWidth;
                existing.CardHeight = sourceState.CardHeight ?? existing.CardHeight;
                existing.SourceEtag = sourceState.SourceEtag ?? existing.SourceEtag;
                existing.SourceLastModifiedUtc = sourceState.SourceLastModifiedUtc ?? existing.SourceLastModifiedUtc;
                existing.SourceContentLength = sourceState.SourceContentLength ?? existing.SourceContentLength;
                existing.ChecksumSha256 = sourceState.ChecksumSha256 ?? existing.ChecksumSha256;

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
                existing.CardGeneratedCount = summary.CardGeneratedCount;
                existing.CardReplacedCount = summary.CardReplacedCount;
                existing.FailedCount = summary.FailedCount;
                existing.SummaryJson = JsonConvert.SerializeObject(summary.Changes);
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Saved apartment media sync run {RunId}. Status={Status}, ApartmentsProcessed={ApartmentsProcessed}, Downloaded={Downloaded}, Replaced={Replaced}, Deleted={Deleted}, SequenceUpdated={SequenceUpdated}, CardGenerated={CardGenerated}, CardReplaced={CardReplaced}, Failed={Failed}.",
                summary.RunId,
                summary.Status,
                summary.ApartmentsProcessed,
                summary.DownloadedCount,
                summary.ReplacedCount,
                summary.DeletedCount,
                summary.SequenceUpdatedCount,
                summary.CardGeneratedCount,
                summary.CardReplacedCount,
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
                CardGeneratedCount = summary.CardGeneratedCount,
                CardReplacedCount = summary.CardReplacedCount,
                FailedCount = summary.FailedCount,
                SummaryJson = JsonConvert.SerializeObject(summary.Changes)
            };
        }

        private ObjectMedium MapAssetToObjectMedium(ApartmentMediaAssetEntity asset)
        {
            return new ObjectMedium
            {
                Id = asset.IdoObjectMediaId ?? asset.Id,
                ObjectId = asset.ApartmentId,
                Url = _blobStorage.BuildBlobUrl(asset.StorageKey),
                CardUrl = string.IsNullOrWhiteSpace(asset.CardStorageKey)
                    ? _blobStorage.BuildBlobUrl(asset.StorageKey)
                    : _blobStorage.BuildBlobUrl(asset.CardStorageKey),
                Extension = asset.Extension,
                Position = asset.PictureDisplaySequence,
                Type = asset.ContentType
            };
        }
    }
}
