using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RentoomBooking.SharedClasses.Integrations.RentoomApp.ArrivalInstructions
{
    public class ArrivalInstructionsService
    {
        private readonly IDbContextFactory<RappInstructionsDbContext> _dbContextFactory;

        public ArrivalInstructionsService(IDbContextFactory<RappInstructionsDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task<IReadOnlyList<ApartmentArrivalInstructionStepDTO>> GetArrivalInstructionStepsAsync(
            int apartmentId,
            CancellationToken cancellationToken = default)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await dbContext.ArrivalInstructions
                .AsNoTracking()
                .Where(step => step.ApartmentId == apartmentId)
                .OrderBy(step => step.Sequence)
                .Select(step => new ApartmentArrivalInstructionStepDTO
                {
                    Id = step.Id,
                    ApartmentId = step.ApartmentId,
                    Sequence = step.Sequence,
                    Name = step.Name,
                    Description = step.Description,
                    ImageMediaAssetId = step.ImageMediaAssetId,
                    ImageUrl = step.ImageUrl
                })
                .ToListAsync(cancellationToken);
        }
    }
}
