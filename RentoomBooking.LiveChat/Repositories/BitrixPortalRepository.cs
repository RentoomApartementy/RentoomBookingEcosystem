using Microsoft.EntityFrameworkCore;
using RentoomBooking.Api.LiveChat.Data;
using RentoomBooking.Api.LiveChat.Entities;

namespace RentoomBooking.Api.LiveChat.Repositories;

public sealed class BitrixPortalRepository : IBitrixPortalRepository
{
    private readonly IDbContextFactory<LiveChatDbContext> _dbContextFactory;

    public BitrixPortalRepository(IDbContextFactory<LiveChatDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<BitrixLiveChatPortalEntity?> GetByMemberIdAsync(string memberId, bool trackChanges = false, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.BitrixLiveChatPortals.Where(p => p.MemberId == memberId);
        if (!trackChanges) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<BitrixLiveChatPortalEntity?> GetByDomainAsync(string domain, bool trackChanges = false, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = db.BitrixLiveChatPortals.Where(p => p.Domain == domain);
        if (!trackChanges) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<List<BitrixLiveChatPortalEntity>> GetAllAsync(bool trackChanges = false, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var query = (IQueryable<BitrixLiveChatPortalEntity>)db.BitrixLiveChatPortals;
        if (!trackChanges) query = query.AsNoTracking();
        return await query.ToListAsync(ct);
    }

    public async Task<BitrixLiveChatPortalEntity> UpsertAsync(BitrixLiveChatPortalEntity portal, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

        var existing = await db.BitrixLiveChatPortals
            .FirstOrDefaultAsync(p => p.MemberId == portal.MemberId || p.Domain == portal.Domain, ct);

        if (existing is null)
        {
            db.BitrixLiveChatPortals.Add(portal);
            await db.SaveChangesAsync(ct);
            return portal;
        }

        existing.MemberId = portal.MemberId;
        existing.Domain = portal.Domain;
        existing.ClientEndpoint = portal.ClientEndpoint;
        existing.ServerEndpoint = portal.ServerEndpoint;
        existing.AccessToken = portal.AccessToken;
        existing.RefreshToken = portal.RefreshToken;
        existing.Scope = portal.Scope;
        existing.Status = portal.Status;
        existing.ApplicationToken = portal.ApplicationToken;
        existing.AccessTokenExpiresAt = portal.AccessTokenExpiresAt;
        existing.EventHandlerId = portal.EventHandlerId;
        existing.EventHandlerUrl = portal.EventHandlerUrl;
        existing.UpdatedAt = portal.UpdatedAt;

        await db.SaveChangesAsync(ct);
        return existing;
    }
}
