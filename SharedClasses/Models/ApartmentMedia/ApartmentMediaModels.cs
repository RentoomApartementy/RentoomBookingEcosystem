using RentoomBooking.SharedClasses.Models.IdoBooking;

namespace RentoomBooking.SharedClasses.Models.ApartmentMedia
{
    public sealed class ApartmentMediaAssetDto
    {
        public int Id { get; set; }
        public int ApartmentId { get; set; }
        public int? IdoObjectMediaId { get; set; }
        public string IdoSourceUrl { get; set; } = string.Empty;
        public string StorageKey { get; set; } = string.Empty;
        public string? BlobUrl { get; set; }
        public string? ContentType { get; set; }
        public string? Extension { get; set; }
        public int PictureDisplaySequence { get; set; }
        public string? CardStorageKey { get; set; }
        public string? CardBlobUrl { get; set; }
        public string? CardContentType { get; set; }
        public int? CardWidth { get; set; }
        public int? CardHeight { get; set; }
        public string? SourceEtag { get; set; }
        public DateTime? SourceLastModifiedUtc { get; set; }
        public long? SourceContentLength { get; set; }
        public string? ChecksumSha256 { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class ApartmentMediaSyncChange
    {
        public int ApartmentId { get; set; }
        public string IdoSourceUrl { get; set; } = string.Empty;
        public string? StorageKey { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public string? Reason { get; set; }
        public string? ContentType { get; set; }
        public long? SizeBytes { get; set; }
        public int? OldSequence { get; set; }
        public int? NewSequence { get; set; }
        public string? Error { get; set; }
    }

    public sealed class ApartmentMediaSyncRunSummary
    {
        public Guid RunId { get; set; } = Guid.NewGuid();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; } = "running";
        public int ApartmentsProcessed { get; set; }
        public int MediaItemsSeen { get; set; }
        public int DownloadedCount { get; set; }
        public int ReplacedCount { get; set; }
        public int DeletedCount { get; set; }
        public int SequenceUpdatedCount { get; set; }
        public int CardGeneratedCount { get; set; }
        public int CardReplacedCount { get; set; }
        public int FailedCount { get; set; }
        public List<ApartmentMediaSyncChange> Changes { get; set; } = new();
    }

    public sealed class ApartmentMediaSyncSourceState
    {
        public ObjectMedium SourceMedium { get; set; } = new();
        public int PictureDisplaySequence { get; set; }
        public string? ContentType { get; set; }
        public string? SourceEtag { get; set; }
        public DateTime? SourceLastModifiedUtc { get; set; }
        public long? SourceContentLength { get; set; }
        public string? ChecksumSha256 { get; set; }
        public string? CardStorageKey { get; set; }
        public string? CardContentType { get; set; }
        public int? CardWidth { get; set; }
        public int? CardHeight { get; set; }
    }
}
