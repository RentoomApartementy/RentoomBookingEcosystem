using Microsoft.EntityFrameworkCore;
using RentoomBooking.LiveChat.Data;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Repositories;

public sealed class LiveChatMessageRepository : ILiveChatMessageRepository
{
    private readonly IDbContextFactory<LiveChatDbContext> _dbContextFactory;

    public LiveChatMessageRepository(IDbContextFactory<LiveChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<LiveChatMessageEntity>> GetBySessionAsync(Guid sessionId, DateTime? after = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.LiveChatMessages
            .AsNoTracking()
            .Where(m => m.SessionId == sessionId);

        if (after.HasValue)
            query = query.Where(m => m.CreatedAt > after.Value);

        return await query.OrderBy(m => m.CreatedAt).ToListAsync(ct);
    }

    public async Task<LiveChatMessageEntity?> GetByBitrixMessageIdAsync(string bitrixMessageId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.LiveChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.BitrixMessageId == bitrixMessageId, ct);
    }

    public async Task<LiveChatMessageEntity> CreateAsync(LiveChatMessageEntity message, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        db.LiveChatMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }

    public async Task UpdateAsync(LiveChatMessageEntity message, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        db.LiveChatMessages.Update(message);
        await db.SaveChangesAsync(ct);
    }
}