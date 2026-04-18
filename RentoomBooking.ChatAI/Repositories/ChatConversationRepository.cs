using Microsoft.EntityFrameworkCore;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Data;
using RentoomBooking.ChatAI.Entities;

namespace RentoomBooking.ChatAI.Repositories;

public sealed class ChatConversationRepository : IChatConversationRepository
{
    private readonly IDbContextFactory<ChatAIDbContext> _dbContextFactory;

    public ChatConversationRepository(IDbContextFactory<ChatAIDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ChatConversationEntity?> GetByIdAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
    }

    public async Task<ChatConversationEntity> CreateAsync(string reservationToken, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new ChatConversationEntity
        {
            Id = Guid.NewGuid(),
            ReservationToken = reservationToken,
            Status = ChatConversationStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.ChatConversations.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task TouchAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ChatConversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }
}
