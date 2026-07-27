using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.NearbyAttractions
{
    public class ApartmentNearbyAttractionsService
    {
        private readonly IDbContextFactory<RappNearbyAttractionsDbContext> _dbContextFactory;

        public ApartmentNearbyAttractionsService(IDbContextFactory<RappNearbyAttractionsDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        /// <summary>Web: dokładne dopasowanie po kluczu głównym (ApartmentItemId = apartment.Items[0].Id).</summary>
        public async Task<NearbyAttractionsResultDTO?> GetNearbyAttractionsAsync(int apartmentItemId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var set = await db.ApartmentNearbyAttractionsSets
                .AsNoTracking()
                .Include(s => s.Attractions)
                .FirstOrDefaultAsync(s => s.ApartmentItemId == apartmentItemId, ct);

            return set == null ? null : MapToDto(set);
        }

        /// <summary>Api/StayWell: po IdoSell object id. Przy wielu itemach bierzemy najświeższy set.</summary>
        public async Task<NearbyAttractionsResultDTO?> GetNearbyAttractionsByObjectIdAsync(int objectId, CancellationToken ct = default)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var set = await db.ApartmentNearbyAttractionsSets
                .AsNoTracking()
                .Include(s => s.Attractions)
                .Where(s => s.ObjectId == objectId)
                .OrderByDescending(s => s.LastRefreshedUtc)
                .FirstOrDefaultAsync(ct);

            return set == null ? null : MapToDto(set);
        }

        private static NearbyAttractionsResultDTO MapToDto(ApartmentNearbyAttractionsSet set)
        {
            return new NearbyAttractionsResultDTO
            {
                ApartmentItemId = set.ApartmentItemId,
                LastRefreshedUtc = set.LastRefreshedUtc,
                Status = set.LastRefreshStatus,
                Items = set.Attractions
                    .Where(a => a.RentoomWebsiteEnabled)   // pokazujemy tylko atrakcje włączone dla strony
                    .OrderBy(a => a.DistanceMeters)
                    .Select(a => new NearbyAttractionDto
                    {
                        Name = a.Name,
                        Category = a.Category,
                        DistanceMeters = a.DistanceMeters,
                        WalkMinutes = a.WalkMinutes,
                        Address = a.Address,
                        Rating = a.Rating,
                        GoogleMapsUri = a.GoogleMapsUri,
                        ExternalPlaceId = a.ExternalPlaceId,
                        RentoomWebsiteEnabled = a.RentoomWebsiteEnabled
                    })
                    .ToList()
            };
        }
    }
}
