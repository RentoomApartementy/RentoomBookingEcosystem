using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Npgsql;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models.ApartmentMedia;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;
using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Services.ApartmentMedia
{
    public interface IApartmentMediaCatalogService
    {
        /// <param name="culture">Culture for the alt texts. Null falls back to <see cref="CultureInfo.CurrentUICulture"/>.</param>
        Task<List<ObjectMedium>> GetApartmentMediaAsync(int apartmentId, string? culture = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyDictionary<int, List<ObjectMedium>>> GetApartmentMediaBatchAsync(
            IReadOnlyCollection<int> apartmentIds,
            string? culture = null,
            CancellationToken cancellationToken = default);
        Task<List<ApartmentMediaAssetEntity>> GetAssetEntitiesAsync(int apartmentId, CancellationToken cancellationToken = default);
        Task UpsertAssetsAsync(
            int apartmentId,
            IReadOnlyCollection<ApartmentMediaSyncSourceState> sourceStates,
            ApartmentMediaSyncRunSummary summary,
            IReadOnlyDictionary<string, ApartmentMediaDuplicateSource>? duplicateSources = null,
            CancellationToken cancellationToken = default);
        Task SaveRunSummaryAsync(ApartmentMediaSyncRunSummary summary, CancellationToken cancellationToken = default);
    }

    public sealed class ApartmentMediaCatalogService : IApartmentMediaCatalogService
    {
        private static readonly IReadOnlyDictionary<int, string> EmptyAltTexts = new Dictionary<int, string>();

        // Set once the alt-text table turns out to be absent, so a missing deployment does not cost a
        // failing query on every media fetch. Cleared by an application restart.
        private static volatile bool _altTextsTableMissing;

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

        public async Task<List<ObjectMedium>> GetApartmentMediaAsync(int apartmentId, string? culture = null, CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entities = await context.ApartmentMediaAssets
                .AsNoTracking()
                .Where(asset => asset.ApartmentId == apartmentId)
                .OrderBy(asset => asset.PictureDisplaySequence)
                .ThenBy(asset => asset.Id)
                .ToListAsync(cancellationToken);

            var altTexts = await LoadAltTextsAsync(context, entities, culture, cancellationToken);

            return entities.Select(asset => MapAssetToObjectMedium(asset, altTexts)).ToList();
        }

        public async Task<IReadOnlyDictionary<int, List<ObjectMedium>>> GetApartmentMediaBatchAsync(
            IReadOnlyCollection<int> apartmentIds,
            string? culture = null,
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

            var altTexts = await LoadAltTextsAsync(context, entities, culture, cancellationToken);

            var mediaByApartmentId = requestedApartmentIds.ToDictionary(
                apartmentId => apartmentId,
                _ => new List<ObjectMedium>());

            foreach (var entity in entities)
            {
                mediaByApartmentId[entity.ApartmentId].Add(MapAssetToObjectMedium(entity, altTexts));
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
            IReadOnlyDictionary<string, ApartmentMediaDuplicateSource>? duplicateSources = null,
            CancellationToken cancellationToken = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existingAssets = await context.ApartmentMediaAssets
                .Where(asset => asset.ApartmentId == apartmentId)
                .ToListAsync(cancellationToken);

            var sourceMap = sourceStates.ToDictionary(state => state.SourceMedium.Url ?? string.Empty, StringComparer.Ordinal);
            duplicateSources ??= new Dictionary<string, ApartmentMediaDuplicateSource>(StringComparer.Ordinal);
            var utcNow = DateTime.UtcNow;
            var assetsToDelete = existingAssets
                .Where(asset => !sourceMap.ContainsKey(asset.IdoSourceUrl))
                .ToList();
            var deletedAssetIds = assetsToDelete.Select(asset => asset.Id).ToHashSet();
            var retainedBlobKeys = existingAssets
                .Where(asset => !deletedAssetIds.Contains(asset.Id))
                .SelectMany(asset => new[] { asset.StorageKey, asset.CardStorageKey })
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);

            foreach (var asset in assetsToDelete)
            {
                var isDuplicate = duplicateSources.TryGetValue(asset.IdoSourceUrl, out var duplicate);
                var reason = isDuplicate ? "duplicate_checksum" : "source_removed";

                if (!retainedBlobKeys.Contains(asset.StorageKey))
                {
                    await _blobStorage.DeleteIfExistsAsync(asset.StorageKey, cancellationToken);
                    summary.Changes.Add(new ApartmentMediaSyncChange
                    {
                        ApartmentId = apartmentId,
                        IdoSourceUrl = asset.IdoSourceUrl,
                        StorageKey = asset.StorageKey,
                        Action = "deleted",
                        Variant = "original",
                        Reason = reason,
                        OldSequence = asset.PictureDisplaySequence,
                        ContentType = asset.ContentType,
                        ChecksumSha256 = duplicate?.ChecksumSha256,
                        RetainedIdoSourceUrl = duplicate?.RetainedIdoSourceUrl
                    });

                    _logger.LogInformation(
                        "Apartment media asset deleted. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, DeletedIdoSourceUrl={DeletedIdoSourceUrl}, RetainedIdoSourceUrl={RetainedIdoSourceUrl}, ChecksumSha256={ChecksumSha256}, StorageKey={StorageKey}, Variant={Variant}, Reason={Reason}, OldSequence={OldSequence}, ContentType={ContentType}",
                        summary.RunId,
                        apartmentId,
                        asset.IdoSourceUrl,
                        duplicate?.RetainedIdoSourceUrl,
                        duplicate?.ChecksumSha256,
                        asset.StorageKey,
                        "original",
                        reason,
                        asset.PictureDisplaySequence,
                        asset.ContentType);
                }
                else
                {
                    _logger.LogWarning(
                        "Skipping deletion of shared apartment media blob. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, IdoSourceUrl={IdoSourceUrl}, StorageKey={StorageKey}, Variant={Variant}",
                        summary.RunId,
                        apartmentId,
                        asset.IdoSourceUrl,
                        asset.StorageKey,
                        "original");
                }

                if (!string.IsNullOrWhiteSpace(asset.CardStorageKey) && !retainedBlobKeys.Contains(asset.CardStorageKey))
                {
                    await _blobStorage.DeleteIfExistsAsync(asset.CardStorageKey, cancellationToken);
                    summary.Changes.Add(new ApartmentMediaSyncChange
                    {
                        ApartmentId = apartmentId,
                        IdoSourceUrl = asset.IdoSourceUrl,
                        StorageKey = asset.CardStorageKey,
                        Action = "deleted",
                        Variant = "card",
                        Reason = reason,
                        OldSequence = asset.PictureDisplaySequence,
                        ContentType = asset.CardContentType,
                        ChecksumSha256 = duplicate?.ChecksumSha256,
                        RetainedIdoSourceUrl = duplicate?.RetainedIdoSourceUrl
                    });

                    _logger.LogInformation(
                        "Apartment media asset deleted. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, DeletedIdoSourceUrl={DeletedIdoSourceUrl}, RetainedIdoSourceUrl={RetainedIdoSourceUrl}, ChecksumSha256={ChecksumSha256}, StorageKey={StorageKey}, Variant={Variant}, Reason={Reason}, OldSequence={OldSequence}, ContentType={ContentType}",
                        summary.RunId,
                        apartmentId,
                        asset.IdoSourceUrl,
                        duplicate?.RetainedIdoSourceUrl,
                        duplicate?.ChecksumSha256,
                        asset.CardStorageKey,
                        "card",
                        reason,
                        asset.PictureDisplaySequence,
                        asset.CardContentType);
                }

                context.ApartmentMediaAssets.Remove(asset);
                summary.DeletedCount++;
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

        /// <summary>
        /// Loads the alt texts for the given assets in a single query. The SQL filter keeps both the
        /// requested culture family and the default one, so <see cref="AltTextCultureResolver.SelectBest"/>
        /// can apply its full fallback chain without pulling every culture from the table.
        /// </summary>
        private async Task<IReadOnlyDictionary<int, string>> LoadAltTextsAsync(
            PostgresBookingDbContext context,
            IReadOnlyCollection<ApartmentMediaAssetEntity> assets,
            string? culture,
            CancellationToken cancellationToken)
        {
            if (assets.Count == 0 || _altTextsTableMissing)
            {
                return EmptyAltTexts;
            }

            var normalizedCulture = AltTextCultureResolver.NormalizeCulture(culture ?? CultureInfo.CurrentUICulture.Name);
            var neutralCulture = AltTextCultureResolver.GetNeutralCulture(normalizedCulture);
            var neutralDefaultCulture = AltTextCultureResolver.GetNeutralCulture(AltTextCultureResolver.DefaultCulture);
            var neutralPrefix = neutralCulture + "-";
            var neutralDefaultPrefix = neutralDefaultCulture + "-";

            var assetIds = assets.Select(asset => asset.Id).ToList();

            List<ApartmentMediaAltText> rows;
            try
            {
                rows = await context.ApartmentMediaAltTexts
                    .AsNoTracking()
                    .Where(alt => assetIds.Contains(alt.MediaAssetId))
                    .Where(alt => alt.Culture == neutralCulture
                        || alt.Culture.StartsWith(neutralPrefix)
                        || alt.Culture == neutralDefaultCulture
                        || alt.Culture.StartsWith(neutralDefaultPrefix))
                    .ToListAsync(cancellationToken);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // The table is owned by the RentoomApp repository and may not be deployed yet. Photos must
                // keep loading with their generated alt text instead of failing the whole media fetch.
                _altTextsTableMissing = true;
                _logger.LogWarning(ex, "apartment_media_alt_texts is missing; falling back to generated alt texts.");
                return EmptyAltTexts;
            }

            if (rows.Count == 0)
            {
                return EmptyAltTexts;
            }

            var result = new Dictionary<int, string>();
            foreach (var group in rows.GroupBy(alt => alt.MediaAssetId))
            {
                var best = AltTextCultureResolver.SelectBest(group, normalizedCulture);
                if (best != null && !string.IsNullOrWhiteSpace(best.AltText))
                {
                    result[group.Key] = best.AltText.Trim();
                }
            }

            return result;
        }

        private ObjectMedium MapAssetToObjectMedium(
            ApartmentMediaAssetEntity asset,
            IReadOnlyDictionary<int, string> altTexts)
        {
            return new ObjectMedium
            {
                Id = asset.IdoObjectMediaId ?? asset.Id,
                MediaAssetId = asset.Id,
                Alt = altTexts.TryGetValue(asset.Id, out var alt) ? alt : null,
                ObjectId = asset.ApartmentId,
                Url = _blobStorage.BuildBlobUrl(asset.StorageKey),
                CardUrl = string.IsNullOrWhiteSpace(asset.CardStorageKey)
                    ? _blobStorage.BuildBlobUrl(asset.StorageKey)
                    : _blobStorage.BuildBlobUrl(asset.CardStorageKey),
                Width = asset.CardWidth,
                Height = asset.CardHeight,
                Extension = asset.Extension,
                Position = asset.PictureDisplaySequence,
                Type = asset.ContentType
            };
        }
    }
}
