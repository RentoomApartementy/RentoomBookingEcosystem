using Microsoft.EntityFrameworkCore;
using RentoomBooking.ChatAI.Data;
using RentoomBooking.ChatAI.Entities;

namespace RentoomBooking.ChatAI.Repositories;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly IDbContextFactory<ChatAIDbContext> _dbContextFactory;

    public ChatMessageRepository(IDbContextFactory<ChatAIDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<ChatMessageEntity>> GetRecentByConversationAsync(Guid conversationId, int limit, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedLimit = limit < 1 ? 1 : limit;

        var records = await context.ChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        records.Reverse();
        return records;
    }

    public async Task AddAsync(ChatMessageEntity message, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ChatMessageEntity> messages, CancellationToken cancellationToken = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.ChatMessages.AddRange(messages);
        await context.SaveChangesAsync(cancellationToken);
    }
}
