using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RentoomBooking.SharedClasses.Database;
using RentoomBooking.SharedClasses.Models;
using RentoomBooking.SharedClasses.Models.ApartmentMedia;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;

using RentoomBooking.SharedClasses.Models.IdoBooking;
using RentoomBooking.SharedClasses.Models.IdoBooking.ObjectLocationDTO;
using RentoomBooking.SharedClasses.Models.IdoBooking.Public;
using RentoomBooking.SharedClasses.Services.ApartmentMedia;
using RentoomBooking.SharedClasses.Services.BookingDatabaseService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Services.IdoBooking
{
    public interface IIdoApartmentService
    {
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default);
        Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(CancellationToken ct = default);
        Task<List<ObjectMedium>?> GetObjectMediaFromIdoSellAsync(int objectId, CancellationToken ct = default);
        Task<List<ObjectDescription>?> GetObjectDescriptionsAsync(int objectId, string? language = null, CancellationToken ct = default);
        Task<List<ObjectAmenity>?> GetObjectAmenitiesAsync(int objectId, CancellationToken ct = default);
        Task<List<ApartmentObject>> SyncApartmentsAndAmenitiesAsync(CancellationToken ct = default);
        Task<List<ApartmentObject>> SaveAllApartmentsToPostgresAsync(CancellationToken ct = default);

    }
    public class IdoApartmentService : IIdoApartmentService
    {

        //private const string ApartmentsGetEndpoint = "clients/get/34/json";
        private const string PublicParametersGetEndpoint = "public/parameters/34/json";
        private const string ApartmentsLocationGetEndpoint = "objects/getLocation/34/json";
        private const string ApartemntsGetEndpoint = "objects/getAll/34/json";
        private const string ObjectMediaGetEndpoint = "objects/getMedia/34/json";
        private const string ApartmentAmenitiesGetEndpoint = "objects/getAmenities/34/json";
        private const string ObjectDescriptionsGetEndpoint = "objects/getDescriptions/34/json";

        // private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IIdoApartmentService> _logger;
        private readonly IIdoBookingConnectService _idoConnect;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ApartmentRepository _apartmentRepository;
        private readonly PostgresBookingDatabase _postgresBookingDatabase;
        private readonly IApartmentMediaCatalogService _apartmentMediaCatalogService;
        private readonly IApartmentPhotoBlobStorage _apartmentPhotoBlobStorage;

        public IdoApartmentService(
            IIdoBookingConnectService idoConnect,
            IHttpClientFactory httpClientFactory,
            ILogger<IdoApartmentService> logger,
            ApartmentRepository apartmentRepository,
            PostgresBookingDatabase postgresBookingDatabase,
            IApartmentMediaCatalogService apartmentMediaCatalogService,
            IApartmentPhotoBlobStorage apartmentPhotoBlobStorage)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idoConnect = idoConnect;
            _apartmentRepository = apartmentRepository;
            _postgresBookingDatabase = postgresBookingDatabase ?? throw new ArgumentNullException(nameof(postgresBookingDatabase));
            _apartmentMediaCatalogService = apartmentMediaCatalogService ?? throw new ArgumentNullException(nameof(apartmentMediaCatalogService));
            _apartmentPhotoBlobStorage = apartmentPhotoBlobStorage ?? throw new ArgumentNullException(nameof(apartmentPhotoBlobStorage));
        }


        public async Task<GetObjectLocationResponseType> GetObjectLocationsAsync(
           ParamsSearchObjectLocationType? parameters = null,
           CancellationToken ct = default)
        {

            var request = new GetObjectLocationRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ParamsSearchObjectLocation = parameters
            };

            var resp = await _idoConnect.PostAsync<GetObjectLocationRequestType, GetObjectLocationResult>(ApartmentsLocationGetEndpoint, request, ct);
            var x = await GetPublicObjectLocationsAsync(ct);
            return resp.Result;
        }


        public async Task<List<LocalizationItem>?> GetPublicObjectLocationsAsync(CancellationToken ct = default)
        {

            PublicParametersResult? resp = await _idoConnect.PostAsync<object, PublicParametersResult>(PublicParametersGetEndpoint, null, ct);

            return resp?.Result.Locations;
        }

        public async Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellWithLocalizationInfoAsync(CancellationToken ct = default)
        {


            List<LocalizationItem> locs = await GetPublicObjectLocationsAsync(ct);

            List<ApartmentObject> apartments = await GetAllApartmentsFromIdoSellAsync(ct);

            var _params = IdoBookingBaseHelper.BuildObjectLocationParams(apartments);

            GetObjectLocationResponseType Objlocs = await GetObjectLocationsAsync(_params, ct);
            //Objlocs.ObjectLocations

            Objlocs.ObjectLocations.ForEach(a => a.LocalizationItem = locs?.FirstOrDefault(loc => loc.Id == a.LocationId));

            apartments?.ForEach(a => a.ObjectLocation = Objlocs.ObjectLocations?.FirstOrDefault(l => l.ObjectId == a.Id));

            await _apartmentRepository.SaveApartmentsAsync(apartments, _logger, ct);

            return apartments;
        }

        public async Task<List<ApartmentObject>> SaveAllApartmentsToPostgresAsync(CancellationToken ct = default)
        {
            List<LocalizationItem> locs = await GetPublicObjectLocationsAsync(ct);

            List<ApartmentObject> apartments = await GetAllApartmentsFromIdoSellAsync(ct);

            var parameters = IdoBookingBaseHelper.BuildObjectLocationParams(apartments);

            GetObjectLocationResponseType objLocs = await GetObjectLocationsAsync(parameters, ct);

            objLocs.ObjectLocations.ForEach(a => a.LocalizationItem = locs?.FirstOrDefault(loc => loc.Id == a.LocationId));

            apartments?.ForEach(a => a.ObjectLocation = objLocs.ObjectLocations?.FirstOrDefault(l => l.ObjectId == a.Id));

            await _postgresBookingDatabase.SaveApartmentsAsync(apartments, _logger, ct);

            return apartments;
        }




        public async Task<List<ApartmentObject>> GetAllApartmentsFromIdoSellAsync(CancellationToken ct = default)
        {
            List<ApartmentObject> retList = new();

            int currentPage = 1;
            int pageAll = 1;

            do
            {
                var ret = await GetApartmentsByPageFromIdoSellAsync(currentPage);

                pageAll = ret.Result?.Result?.PageAll ?? 0;

                if (pageAll == 0)
                {
                    _logger.LogWarning("apiResponse.Result or apiResponse.Result.Result is null, or PageAll is 0. Ending sync.");
                    break;
                }

                retList.AddRange(ret.Result.Objects);

                _logger.LogInformation($"Number of apartments fetched so far: {retList.Count}");

                currentPage++;

            } while (currentPage <= pageAll);
            return retList;
        }


        public async Task<ApartmentResponseType> GetApartmentsByPageFromIdoSellAsync(int page, CancellationToken ct = default)
        {

            var request = new ApartmentRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                Result = new ResultRequestPaging { Page = page, Number = 100 },
            };

            ApartmentResponseType? resp = await _idoConnect.PostAsync<ApartmentRequestType, ApartmentResponseType>(ApartemntsGetEndpoint, request, ct);
            return resp;



        }


        public async Task<List<ObjectMedium>?> GetObjectMediaFromIdoSellAsync(int objectId, CancellationToken ct = default)
        {
            var request = new ObjectMediaRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectId = objectId
            };
            var ret =  await _idoConnect.PostAsync<ObjectMediaRequestType, ObjectMediaResponseType>(ObjectMediaGetEndpoint, request, ct);
            return ret?.Result.ObjectMedia;
        }

        public async Task<List<ObjectDescription>?> GetObjectDescriptionsAsync(int objectId, string? language = null, CancellationToken ct = default)
        {
            var lang = language != "pol" && language != "eng" ? "eng" : language;
            var request = new ObjectDescriptionsRequestType

            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ParamsSearch = new ObjectDescriptionParamsSearch
                {
                    ObjectId = objectId,
                    Language = lang,
                }
            };

            var ret = await _idoConnect.PostAsync<ObjectDescriptionsRequestType, ObjectDescriptionsResponseType>(ObjectDescriptionsGetEndpoint, request, ct);
            return ret?.Result.ObjectDescriptions;
        }


        public async Task<List<ObjectAmenity>?> GetObjectAmenitiesAsync(int objectId, CancellationToken cancellationToken = default)
        {

            _logger.LogInformation("Fetching amenities for object {ObjectId}", objectId);

            var request = new ObjectAmenitiesRequestType
            {
                Authenticate = _idoConnect.AuthObjectIdo(),
                ObjectId = objectId
            };
            var ret = await _idoConnect.PostAsync<ObjectAmenitiesRequestType, ObjectAmenitiesResponseType>(ApartmentAmenitiesGetEndpoint, request, cancellationToken);

            /*
               if (!response.IsSuccessStatusCode)
               {
                   _logger.LogError("Failed to fetch media for object {ObjectId}. StatusCode: {StatusCode}. Content: {Content}", objectId, response.StatusCode, responseContent);
                   response.EnsureSuccessStatusCode();
               }
            */
            // ObjectAmenitiesResponseType ret = JsonConvert.DeserializeObject<ObjectAmenitiesResponseType>(responseContent);
            return ret?.Result.ObjectAmenities;
        }


        public async Task<List<ApartmentObject>> SyncApartmentsAndAmenitiesAsync(CancellationToken ct = default)
        {
            var apartments = await SaveAllApartmentsToPostgresAsync(ct);

            var amenitiesDocuments = new List<ApartmentAmenitiesDocument>(apartments.Count);

            foreach (var apartment in apartments)
            {
                ct.ThrowIfCancellationRequested();

                var amenities = await GetObjectAmenitiesAsync(apartment.Id, ct) ?? new List<ObjectAmenity>();

                var document = new ApartmentAmenitiesDocument
                {
                    Id = apartment.Id,
                    ApartmentId = apartment.Id,
                  //  Apartment = apartment,
                    Amenities = amenities
                };

                amenitiesDocuments.Add(document);
            }

            _logger.LogInformation("Retrieved amenities for {Count} apartments.", amenitiesDocuments.Count);

            await _apartmentRepository.SaveApartmentAmenitiesAsync(amenitiesDocuments, _logger, ct);
            await SyncApartmentMediaAssetsAsync(apartments, ct);

            return apartments;// amenitiesDocuments;
        }

        private async Task SyncApartmentMediaAssetsAsync(List<ApartmentObject> apartments, CancellationToken ct)
        {
            var summary = new ApartmentMediaSyncRunSummary
            {
                StartedAt = DateTime.UtcNow,
                Status = "running"
            };

            try
            {
                foreach (var apartment in apartments)
                {
                    ct.ThrowIfCancellationRequested();
                    summary.ApartmentsProcessed++;

                    var sourceMedia = (await GetObjectMediaFromIdoSellAsync(apartment.Id, ct) ?? new List<ObjectMedium>())
                        .Where(IsImageMedia)
                        .ToList();

                    summary.MediaItemsSeen += sourceMedia.Count;

                    var existingAssets = await _apartmentMediaCatalogService.GetAssetEntitiesAsync(apartment.Id, ct);
                    var syncStates = new List<ApartmentMediaSyncSourceState>(sourceMedia.Count);

                    for (var index = 0; index < sourceMedia.Count; index++)
                    {
                        var medium = sourceMedia[index];
                        var sourceUrl = medium.Url?.Trim();

                        if (string.IsNullOrWhiteSpace(sourceUrl))
                        {
                            continue;
                        }

                        var sequence = index + 1;
                        var existingAsset = existingAssets.FirstOrDefault(asset => asset.IdoSourceUrl == sourceUrl);
                        var remoteMetadata = await FetchRemoteMetadataAsync(sourceUrl, ct);
                        var shouldDownload = existingAsset is null || HasRemoteContentChanged(existingAsset, remoteMetadata);

                        if (!shouldDownload)
                        {
                            _logger.LogInformation(
                                "Apartment media sync item unchanged. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, IdoSourceUrl={IdoSourceUrl}, StorageKey={StorageKey}, Action={Action}, NewSequence={NewSequence}, SourceEtag={SourceEtag}, SizeBytes={SizeBytes}",
                                summary.RunId,
                                apartment.Id,
                                sourceUrl,
                                existingAsset!.StorageKey,
                                "unchanged",
                                sequence,
                                remoteMetadata.SourceEtag,
                                remoteMetadata.SourceContentLength);
                        }
                        else
                        {
                            var downloadResult = await DownloadRemoteMediaAsync(sourceUrl, ct);
                            var storageKey = existingAsset?.StorageKey ?? _apartmentPhotoBlobStorage.BuildStorageKey(apartment.Id, sourceUrl, medium.Extension);

                            await using (downloadResult.Content)
                            {
                                await _apartmentPhotoBlobStorage.UploadAsync(storageKey, downloadResult.Content, downloadResult.ContentType, ct);
                            }

                            remoteMetadata.ContentType = downloadResult.ContentType ?? remoteMetadata.ContentType;
                            remoteMetadata.ChecksumSha256 = downloadResult.ChecksumSha256;

                            var action = existingAsset is null ? "downloaded" : "replaced";
                            if (existingAsset is null)
                            {
                                summary.DownloadedCount++;
                            }
                            else
                            {
                                summary.ReplacedCount++;
                            }

                            summary.Changes.Add(new ApartmentMediaSyncChange
                            {
                                ApartmentId = apartment.Id,
                                IdoSourceUrl = sourceUrl,
                                StorageKey = storageKey,
                                Action = action,
                                OldSequence = existingAsset?.PictureDisplaySequence,
                                NewSequence = sequence
                            });

                            _logger.LogInformation(
                                "Apartment media sync item processed. RunId={MediaSyncRunId}, ApartmentId={ApartmentId}, IdoSourceUrl={IdoSourceUrl}, StorageKey={StorageKey}, Action={Action}, Reason={Reason}, OldSequence={OldSequence}, NewSequence={NewSequence}, OldEtag={OldEtag}, NewEtag={NewEtag}, SizeBytes={SizeBytes}",
                                summary.RunId,
                                apartment.Id,
                                sourceUrl,
                                storageKey,
                                action,
                                existingAsset is null ? "new_source_url" : "source_metadata_changed",
                                existingAsset?.PictureDisplaySequence,
                                sequence,
                                existingAsset?.SourceEtag,
                                remoteMetadata.SourceEtag,
                                remoteMetadata.SourceContentLength);
                        }

                        syncStates.Add(new ApartmentMediaSyncSourceState
                        {
                            SourceMedium = medium,
                            PictureDisplaySequence = sequence,
                            ContentType = remoteMetadata.ContentType,
                            SourceEtag = remoteMetadata.SourceEtag,
                            SourceLastModifiedUtc = remoteMetadata.SourceLastModifiedUtc,
                            SourceContentLength = remoteMetadata.SourceContentLength,
                            ChecksumSha256 = remoteMetadata.ChecksumSha256
                        });
                    }

                    await _apartmentMediaCatalogService.UpsertAssetsAsync(apartment.Id, syncStates, summary, ct);
                }

                summary.Status = "completed";
            }
            catch (Exception ex)
            {
                summary.Status = "failed";
                summary.FailedCount++;
                _logger.LogError(ex, "Apartment media sync failed. RunId={MediaSyncRunId}", summary.RunId);
                throw;
            }
            finally
            {
                summary.FinishedAt = DateTime.UtcNow;
                await _apartmentMediaCatalogService.SaveRunSummaryAsync(summary, ct);
            }
        }

        private async Task<ApartmentMediaSyncSourceState> FetchRemoteMetadataAsync(string sourceUrl, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, sourceUrl);
            var client = _httpClientFactory.CreateClient();

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    return new ApartmentMediaSyncSourceState();
                }

                return new ApartmentMediaSyncSourceState
                {
                    ContentType = response.Content.Headers.ContentType?.MediaType,
                    SourceEtag = response.Headers.ETag?.Tag,
                    SourceLastModifiedUtc = response.Content.Headers.LastModified?.UtcDateTime,
                    SourceContentLength = response.Content.Headers.ContentLength
                };
            }
            catch
            {
                return new ApartmentMediaSyncSourceState();
            }
        }

        private async Task<(Stream Content, string? ContentType, string? ChecksumSha256)> DownloadRemoteMediaAsync(string sourceUrl, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
            var memoryStream = new MemoryStream();
            await sourceStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var sha256 = SHA256.Create();
            var checksum = Convert.ToHexString(sha256.ComputeHash(memoryStream)).ToLowerInvariant();
            memoryStream.Position = 0;

            return (memoryStream, response.Content.Headers.ContentType?.MediaType, checksum);
        }

        private static bool HasRemoteContentChanged(ApartmentMediaAssetEntity existingAsset, ApartmentMediaSyncSourceState remoteMetadata)
        {
            if (!string.IsNullOrWhiteSpace(remoteMetadata.SourceEtag) &&
                !string.Equals(existingAsset.SourceEtag, remoteMetadata.SourceEtag, StringComparison.Ordinal))
            {
                return true;
            }

            if (remoteMetadata.SourceLastModifiedUtc.HasValue &&
                existingAsset.SourceLastModifiedUtc != remoteMetadata.SourceLastModifiedUtc)
            {
                return true;
            }

            if (remoteMetadata.SourceContentLength.HasValue &&
                existingAsset.SourceContentLength != remoteMetadata.SourceContentLength)
            {
                return true;
            }

            return false;
        }

        private static bool IsImageMedia(ObjectMedium medium)
        {
            var extension = medium.Extension?.Trim().TrimStart('.').ToLowerInvariant();
            return extension is "jpg" or "jpeg" or "png" or "webp" or "gif";
        }

    }
}
