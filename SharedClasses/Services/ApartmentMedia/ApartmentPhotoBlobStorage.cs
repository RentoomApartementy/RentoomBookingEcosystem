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
        string BuildBlobUrl(string storageKey);
        string BuildStorageKey(int apartmentId, string sourceUrl, string? extension);
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
