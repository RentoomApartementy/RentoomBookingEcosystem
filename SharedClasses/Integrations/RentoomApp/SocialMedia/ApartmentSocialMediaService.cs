using Microsoft.EntityFrameworkCore;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Database;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Models;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia
{
    public class ApartmentSocialMediaService
    {
        private readonly IDbContextFactory<RappSocialMediaDbContext> _dbContextFactory;

        public ApartmentSocialMediaService(IDbContextFactory<RappSocialMediaDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<ApartmentSocialMediaDTO?> GetApartmentSocialMediaAsync(int apartmentItemId, CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var entity = await dbContext.ApartmentItemSocialMedia
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApartmentItemId == apartmentItemId, cancellationToken);

            return entity == null ? null : MapToDto(entity);
        }

        private static ApartmentSocialMediaDTO MapToDto(ApartmentItemSocialMedia entity)
        {
            return new ApartmentSocialMediaDTO
            {
                ApartmentItemId = entity.ApartmentItemId,
                YouTubeEmbedCode = entity.YouTubeEmbedCode,
                YouTubeAutoplay = entity.YouTubeAutoplay,
                YouTubeLoop = entity.YouTubeLoop,
                YouTubeMute = entity.YouTubeMute,
                YouTubeControls = entity.YouTubeControls,
                YouTubeModestBranding = entity.YouTubeModestBranding,
                YouTubeWidth = entity.YouTubeWidth,
                YouTubeHeight = entity.YouTubeHeight,
                YouTubeDisplaySize = entity.YouTubeDisplaySize,
                YouTubeVisibleOnRentoom = entity.YouTubeVisibleOnRentoom,
                InstagramEmbedCode = entity.InstagramEmbedCode,
                InstagramVisibleOnRentoom = entity.InstagramVisibleOnRentoom
            };
        }
    }
}
