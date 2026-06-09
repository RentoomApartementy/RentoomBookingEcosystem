using System.Security.Cryptography;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Models.Storage;

namespace RentoomBooking.SharedClasses.Services.ApartmentMedia
{
    public interface IApartmentPhotoBlobStorage
    {
        Task UploadAsync(string storageKey, Stream content, string? contentType, CancellationToken cancellationToken = default);
        Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default);
        Task<(Stream Content, string? ContentType)> DownloadAsync(string storageKey, CancellationToken cancellationToken = default);
        string BuildBlobUrl(string storageKey);
        string BuildStorageKey(int apartmentId, string sourceUrl, string? extension);
        string BuildVariantStorageKey(int apartmentId, string sourceUrl, string variantName, string? extension);
    }

    public sealed class ApartmentPhotoBlobStorage : IApartmentPhotoBlobStorage
    {
        public const string StorageOptionsName = "ApartmentPhotosStorage";
        private readonly BlobContainerClient _containerClient;
        private readonly StorageOptions _storageOptions;

        public ApartmentPhotoBlobStorage(IOptionsMonitor<StorageOptions> storageOptionsMonitor)
        {
            _storageOptions = storageOptionsMonitor.Get(StorageOptionsName);
            _containerClient = CreateContainerClient(_storageOptions);
        }

        public async Task UploadAsync(string storageKey, Stream content, string? contentType, CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            var blobClient = _containerClient.GetBlobClient(storageKey);
            var headers = new BlobHttpHeaders();

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                headers.ContentType = contentType;
            }

            await blobClient.UploadAsync(
                content,
                new BlobUploadOptions { HttpHeaders = headers },
                cancellationToken);
        }

        public async Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            await _containerClient.DeleteBlobIfExistsAsync(storageKey, cancellationToken: cancellationToken);
        }

        public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            var blobClient = _containerClient.GetBlobClient(storageKey);
            var exists = await blobClient.ExistsAsync(cancellationToken);
            return exists.Value;
        }

        public async Task<(Stream Content, string? ContentType)> DownloadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            var blobClient = _containerClient.GetBlobClient(storageKey);
            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
            var buffer = new MemoryStream();
            await response.Value.Content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return (buffer, response.Value.Details.ContentType);
        }

        public string BuildBlobUrl(string storageKey)
        {
            return $"{_containerClient.Uri.AbsoluteUri.TrimEnd('/')}/{storageKey}";
        }

        public string BuildStorageKey(int apartmentId, string sourceUrl, string? extension)
        {
            var normalizedExtension = NormalizeExtension(extension);
            var urlHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceUrl))).ToLowerInvariant();
            return $"apartment-media/{apartmentId}/{urlHash}.{normalizedExtension}";
        }

        public string BuildVariantStorageKey(int apartmentId, string sourceUrl, string variantName, string? extension)
        {
            var normalizedVariantName = string.IsNullOrWhiteSpace(variantName)
                ? "variant"
                : variantName.Trim().ToLowerInvariant();
            var normalizedExtension = NormalizeExtension(extension);
            var urlHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourceUrl))).ToLowerInvariant();
            return $"apartment-media/{apartmentId}/{urlHash}.{normalizedVariantName}.{normalizedExtension}";
        }

        private static BlobContainerClient CreateContainerClient(StorageOptions options)
        {
            var containerName = string.IsNullOrWhiteSpace(options.Container) ? "apartmentsphotos" : options.Container.Trim();

            if (!string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return new BlobContainerClient(options.ConnectionString, containerName);
            }

            if (string.IsNullOrWhiteSpace(options.AccountName))
            {
                throw new InvalidOperationException("Apartment photo storage configuration is missing.");
            }

            var serviceUri = new Uri($"https://{options.AccountName.Trim()}.blob.core.windows.net");
            var serviceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
            return serviceClient.GetBlobContainerClient(containerName);
        }

        private static string NormalizeExtension(string? extension)
        {
            var value = extension?.Trim().TrimStart('.').ToLowerInvariant();
            return string.IsNullOrWhiteSpace(value) ? "bin" : value;
        }
    }
}
