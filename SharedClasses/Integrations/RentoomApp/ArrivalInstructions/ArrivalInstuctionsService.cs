using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Models.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions
{
    public class ArrivalInstructionsService
    {
        private const string DefaultLanguage = "default";
        private readonly IDbContextFactory<RappInstructionsDbContext> _dbContextFactory;
        private readonly IOptionsMonitor<StorageOptions> _storageOptionsMonitor;

        public ArrivalInstructionsService(
            IDbContextFactory<RappInstructionsDbContext> dbContextFactory,
            IOptionsMonitor<StorageOptions> storageOptionsMonitor)
        {
            _dbContextFactory = dbContextFactory;
            _storageOptionsMonitor = storageOptionsMonitor;
        }

        public async Task<IReadOnlyList<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(
            int apartmentId,
            string? language = null,
            CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var normalizedLanguage = NormalizeLanguage(language);

            var baseQuery = dbContext.ArrivalInstructions
                .AsNoTracking()
                .Where(step => step.ApartmentItemId == apartmentId);

            if (!string.Equals(normalizedLanguage, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                var languageSteps = await baseQuery
                    .Where(step => step.Language.ToLower() == normalizedLanguage)
                    .OrderBy(step => step.Sequence)
                    .ToListAsync(cancellationToken);

                if (languageSteps.Count > 0)
                {
                    return languageSteps
                        .Select(MapToDto)
                        .ToList();
                }
            }

            var defaultSteps = await baseQuery
                .Where(step => step.Language.ToLower() == DefaultLanguage)
                .OrderBy(step => step.Sequence)
                .ToListAsync(cancellationToken);

            return defaultSteps
                .Select(MapToDto)
                .ToList();
        }

        private static string NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return DefaultLanguage;
            }

            var trimmed = language.Trim();
            if (string.Equals(trimmed, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return DefaultLanguage;
            }

            var lowered = trimmed.ToLowerInvariant()
                .Replace('_', '-');

            var dashIndex = lowered.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                lowered = lowered[..dashIndex];
            }

            return lowered switch
            {
                "pl" => "pl",
                "pol" => "pl",
                "en" => "en",
                "eng" => "en",
                "de" => "de",
                "deu" => "de",
                "iv" => DefaultLanguage,
                _ => DefaultLanguage
            };
        }

        private ApartmentArrivalInstructionStepDTO MapToDto(ApartmentArrivalInstructionStep step)
        {
            return new ApartmentArrivalInstructionStepDTO
            {
                Id = step.Id,
                ApartmentItemId = step.ApartmentItemId,
                Sequence = step.Sequence,
                Language = step.Language,
                Name = step.Name,
                Description = step.Description,
                ImageMediaAssetId = step.ImageMediaAssetId,
                ImageUrl = ResolveImageUrl(step.ImageUrl)
            };
        }

        private string? ResolveImageUrl(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                return imageUrl;
            }

            var instructionsStorage = _storageOptionsMonitor.Get("InstructionsStorage");
            if (instructionsStorage is null || !instructionsStorage.HasAzureConfiguration())
            {
                return imageUrl;
            }

            var accountName = instructionsStorage.AccountName?.Trim();
            if (string.IsNullOrWhiteSpace(accountName))
            {
                return imageUrl;
            }

            var container = string.IsNullOrWhiteSpace(instructionsStorage.Container)
                ? "uploads"
                : instructionsStorage.Container.Trim().Trim('/');

            var key = imageUrl.Trim();
            if (key.StartsWith("/", StringComparison.Ordinal))
            {
                key = key.TrimStart('/');
            }

            if (!string.IsNullOrWhiteSpace(container)
                && key.StartsWith($"{container}/", StringComparison.OrdinalIgnoreCase))
            {
                key = key[(container.Length + 1)..];
            }

            return $"https://{accountName}.blob.core.windows.net/{container}/{key}";
        }
    }
}
