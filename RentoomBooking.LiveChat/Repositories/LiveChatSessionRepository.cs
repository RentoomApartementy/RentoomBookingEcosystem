using Microsoft.EntityFrameworkCore;
using RentoomBooking.LiveChat.Data;
using RentoomBooking.LiveChat.Entities;

namespace RentoomBooking.LiveChat.Repositories;

public sealed class LiveChatSessionRepository : ILiveChatSessionRepository
{
    private readonly IDbContextFactory<LiveChatDbContext> _dbContextFactory;

    public LiveChatSessionRepository(IDbContextFactory<LiveChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<LiveChatSessionEntity?> GetActiveByReservationTokenAsync(string token,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.LiveChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ReservationToken == token && s.Status == LiveChatSessionStatuses.Active, ct);
    }

    public async Task<LiveChatSessionEntity?> GetActiveByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.LiveChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == LiveChatSessionStatuses.Active, ct);
    }

    public async Task<LiveChatSessionEntity?> GetActiveByBitrixChatIdAsync(string bitrixChatId,
        CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        return await db.LiveChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BitrixChatId == bitrixChatId && s.Status == LiveChatSessionStatuses.Active, ct);
    }

    public async Task<LiveChatSessionEntity> CreateAsync(LiveChatSessionEntity session, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        db.LiveChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task UpdateAsync(LiveChatSessionEntity updated, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var tracked = await db.LiveChatSessions.FirstOrDefaultAsync(s => s.Id == updated.Id, ct);
        if (tracked is null) return;

        tracked.BitrixChatId = updated.BitrixChatId;
        tracked.BitrixSessionId = updated.BitrixSessionId;
        tracked.GuestName = updated.GuestName;
        tracked.GuestEmail = updated.GuestEmail;
        tracked.IdoReservationId = updated.IdoReservationId;
        tracked.ClientBitrixId = updated.ClientBitrixId;
        tracked.DealBitrixId = updated.DealBitrixId;
        tracked.Status = updated.Status;
        tracked.GuestAutoTranslateEnabled = updated.GuestAutoTranslateEnabled;
        tracked.PreferredLanguage = updated.PreferredLanguage;
        tracked.UpdatedAt = updated.UpdatedAt;
        await db.SaveChangesAsync(ct);
    }
}