using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RentoomBooking.LiveChat.Bitrix;
using RentoomBooking.LiveChat.Data;
using RentoomBooking.LiveChat.Entities;
using RentoomBooking.LiveChat.Repositories;
using RentoomBooking.SharedClasses.Configuration;
using RentoomBooking.SharedClasses.LiveChat;
using RentoomBooking.SharedClasses.Models.ReservationWorkflow;
using RentoomBooking.SharedClasses.Services.ReservationWorkflow;

namespace RentoomBooking.LiveChat;

public sealed class BitrixLiveChatService
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Regex NameHeaderRx = new(
        @"\[b\]([^\[]+?):\s*\[/b\]",
        RegexOptions.Compiled);

    private static readonly Regex LeadingBrRx = new(
        @"^\s*(\[br\]\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IBitrixRestClient _bitrixRestClient;
    private readonly string _connectorId;
    private readonly IDbContextFactory<LiveChatDbContext> _dbContextFactory;
    private readonly ILogger<BitrixLiveChatService> _logger;
    private readonly ILiveChatMessageRepository _messageRepo;
    private readonly int _openLineId;
    private readonly IBitrixPortalRepository _portalRepo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILiveChatSessionRepository _sessionRepo;
    private readonly ILiveChatSseSubscriptions _sseSubscriptions;

    public BitrixLiveChatService(
        IBitrixRestClient bitrixRestClient,
        ILiveChatSseSubscriptions sseSubscriptions,
        ILiveChatSessionRepository sessionRepo,
        ILiveChatMessageRepository messageRepo,
        IBitrixPortalRepository portalRepo,
        IDbContextFactory<LiveChatDbContext> dbContextFactory,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime appLifetime,
        IOptions<BitrixLiveChatOptions> options,
        ILogger<BitrixLiveChatService> logger)
    {
        _bitrixRestClient = bitrixRestClient;
        _sseSubscriptions = sseSubscriptions;
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
        _portalRepo = portalRepo;
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
        _appLifetime = appLifetime;
        _logger = logger;
        _connectorId = options.Value.ConnectorId;
        _openLineId = options.Value.OpenLineId;
    }

    public async Task<BitrixLiveChatPortalEntity> InstallPortalAsync(
        BitrixLiveChatPortalInstallation installation,
        Uri webhookUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(webhookUrl);

        var memberId = installation.MemberId.Trim();
        var domain = BitrixRestHelpers.NormalizeDomain(installation.Domain);
        if (string.IsNullOrWhiteSpace(memberId) ||
            string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(installation.AccessToken))
            throw new InvalidOperationException(
                "Bitrix installation payload is missing member_id, domain, or access token.");

        var clientEndpoint = BitrixRestHelpers.NormalizeClientEndpoint(installation.ClientEndpoint, domain);

        await using var db = _dbContextFactory.CreateDbContext();
        var portal = await db.BitrixLiveChatPortals
            .FirstOrDefaultAsync(x => x.MemberId == memberId || x.Domain == domain, ct);

        if (portal is null)
        {
            portal = new BitrixLiveChatPortalEntity();
            db.BitrixLiveChatPortals.Add(portal);
        }

        portal.MemberId = memberId;
        portal.Domain = domain;
        portal.ClientEndpoint = clientEndpoint;
        portal.ServerEndpoint = installation.ServerEndpoint?.Trim();
        portal.AccessToken = installation.AccessToken.Trim();
        portal.RefreshToken = installation.RefreshToken?.Trim();
        portal.Scope = installation.Scope?.Trim();
        portal.Status = installation.Status?.Trim();
        portal.ApplicationToken = installation.ApplicationToken?.Trim();
        portal.AccessTokenExpiresAt = installation.AccessTokenExpiresAt;
        portal.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        var eventHandlerId = await _bitrixRestClient.BindWebhookEventAsync(portal, webhookUrl, ct);
        portal.EventHandlerId = eventHandlerId;
        portal.EventHandlerUrl = webhookUrl.ToString();
        portal.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Installing with connectorId='{ConnectorId}'", _connectorId);
        var placementHandlerUrl = new Uri(
            new Uri($"{webhookUrl.Scheme}://{webhookUrl.Authority}"),
            "/api/staywell/livechat/bitrix-install");
        await _bitrixRestClient.RegisterConnectorAsync(portal, _connectorId, _openLineId, webhookUrl, placementHandlerUrl, ct);
        _logger.LogInformation(
            "Bitrix imconnector registered for domain={Domain}, connectorId={ConnectorId}, openLineId={OpenLineId}",
            portal.Domain, _connectorId, _openLineId);

        return portal;
    }

    public async Task<LiveChatSessionEntity> GetOrCreateSessionAsync(string reservationToken, string? guestName,
        string? guestEmail, CancellationToken ct = default)
    {
        var existing = await _sessionRepo.GetActiveByReservationTokenAsync(reservationToken, ct);

        if (existing is not null)
        {
            var updated = false;
            if (!string.IsNullOrWhiteSpace(guestName) &&
                !string.Equals(existing.GuestName, guestName, StringComparison.Ordinal))
            {
                existing.GuestName = guestName;
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(guestEmail) &&
                !string.Equals(existing.GuestEmail, guestEmail, StringComparison.OrdinalIgnoreCase))
            {
                existing.GuestEmail = guestEmail;
                updated = true;
            }

            updated |= await EnsureSessionReservationContextAsync(existing, ct);
            if (updated)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                await _sessionRepo.UpdateAsync(existing, ct);
            }

            return existing;
        }

        var session = new LiveChatSessionEntity
        {
            ReservationToken = reservationToken,
            GuestName = guestName,
            GuestEmail = guestEmail,
            Status = LiveChatSessionStatuses.Active,
            BitrixChatId = null
        };

        await _sessionRepo.CreateAsync(session, ct);

        if (await EnsureSessionReservationContextAsync(session, ct))
        {
            session.UpdatedAt = DateTime.UtcNow;
            await _sessionRepo.UpdateAsync(session, ct);
        }

        return session;
    }

    public async Task<LiveChatMessageEntity> SendGuestMessageAsync(Guid sessionId, string content,
        CancellationToken ct = default)
    {
        var session = await _sessionRepo.GetActiveByIdAsync(sessionId, ct)
                      ?? throw new InvalidOperationException("Session not found.");

        var message = new LiveChatMessageEntity
        {
            SessionId = sessionId,
            Sender = LiveChatSenders.Guest,
            Content = content,
            SenderName = session.GuestName
        };

        await _messageRepo.CreateAsync(message, ct);
        session.UpdatedAt = DateTime.UtcNow;
        await _sessionRepo.UpdateAsync(session, ct);

        await EnsureSessionReservationContextAsync(session, ct);
        var crmTarget = GetCrmBindingTarget(session);

        await _bitrixRestClient.SendGuestMessageToBitrixAsync(session, message, crmTarget, ct);

        return message;
    }

    public async Task<bool> VerifyWebhookApplicationTokenAsync(string? memberId, string? applicationToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memberId) || string.IsNullOrWhiteSpace(applicationToken)) return false;

        var portal = await _portalRepo.GetByMemberIdAsync(memberId, ct: ct);

        if (portal is null || string.IsNullOrWhiteSpace(portal.ApplicationToken)) return false;

        return string.Equals(portal.ApplicationToken, applicationToken, StringComparison.Ordinal);
    }

    public async Task<LiveChatMessageEntity?> ReceiveOperatorMessageAsync(
        IncomingOperatorMessage incoming,
        CancellationToken ct = default)
    {
        LiveChatSessionEntity? session = null;

        if (!string.IsNullOrWhiteSpace(incoming.BitrixChatId))
            session = await _sessionRepo.GetActiveByBitrixChatIdAsync(incoming.BitrixChatId, ct);

        if (session is null && !string.IsNullOrWhiteSpace(incoming.ConnectorChatId))
            if (incoming.ConnectorChatId.StartsWith("staywell_") &&
                Guid.TryParse(incoming.ConnectorChatId["staywell_".Length..], out var sessionId))
                session = await _sessionRepo.GetActiveByIdAsync(sessionId, ct);

        if (session is null)
        {
            _logger.LogWarning(
                "No active session for ConnectorChatId={ConnectorChatId}, BitrixChatId={BitrixChatId} — message ignored",
                incoming.ConnectorChatId,
                incoming.BitrixChatId);
            return null;
        }

        var sessionUpdated = false;
        if (!string.IsNullOrWhiteSpace(incoming.BitrixChatId) &&
            !string.Equals(session.BitrixChatId, incoming.BitrixChatId, StringComparison.Ordinal))
        {
            session.BitrixChatId = incoming.BitrixChatId;
            sessionUpdated = true;
        }

        if (!string.IsNullOrWhiteSpace(incoming.BitrixMessageId))
        {
            var existing = await _messageRepo.GetByBitrixMessageIdAsync(incoming.BitrixMessageId, ct);
            if (existing is not null)
            {
                if (sessionUpdated)
                {
                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionRepo.UpdateAsync(session, ct);
                }

                _logger.LogDebug("Duplicate operator message ignored: BitrixMessageId={BitrixMessageId}",
                    incoming.BitrixMessageId);
                return existing;
            }
        }

        var (parsedName, cleanText) = ParseBitrixOperatorMessage(incoming.Text ?? string.Empty);

        string? preEnrichedAvatarUrl = null;
        var alreadyEnriched = false;
        if (parsedName is null && !string.IsNullOrWhiteSpace(incoming.AuthorId))
            try
            {
                var info = await _bitrixRestClient.FetchOperatorInfoAsync(incoming.AuthorId, ct);
                parsedName = info.FirstName ?? parsedName;
                preEnrichedAvatarUrl = info.AvatarUrl;
                alreadyEnriched = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to pre-fetch operator info for user {AuthorId}; will fall back to background enrichment",
                    incoming.AuthorId);
            }

        var safeAttachments = FilterAttachments(incoming.Attachments);

        var message = new LiveChatMessageEntity
        {
            SessionId = session.Id,
            Sender = LiveChatSenders.Operator,
            Content = cleanText,
            BitrixMessageId = incoming.BitrixMessageId,
            SenderName = parsedName,
            OperatorBitrixUserId = incoming.AuthorId,
            OperatorAvatarUrl = preEnrichedAvatarUrl,
            Attachments = safeAttachments.Count > 0
                ? JsonSerializer.Serialize(safeAttachments, _jsonOptions)
                : null
        };

        await using var db = _dbContextFactory.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        LiveChatMessageEntity? joinMessage = null;
        var hasRealAuthorId = !string.IsNullOrWhiteSpace(incoming.AuthorId) && incoming.AuthorId != "0";
        if (hasRealAuthorId)
        {
            var agentAlreadyJoined = await db.LiveChatMessages.AnyAsync(
                m => m.SessionId == session.Id && m.OperatorBitrixUserId == incoming.AuthorId, ct);
            if (!agentAlreadyJoined)
            {
                joinMessage = new LiveChatMessageEntity
                {
                    SessionId = session.Id,
                    Sender = LiveChatSenders.System,
                    Content = "agent_joined",
                    SenderName = parsedName
                };
                db.LiveChatMessages.Add(joinMessage);
            }
        }

        db.LiveChatMessages.Add(message);

        var trackedSession = await db.LiveChatSessions.FindAsync(new object[] { session.Id }, ct);
        if (trackedSession is not null)
        {
            if (sessionUpdated)
                trackedSession.BitrixChatId = session.BitrixChatId;
            trackedSession.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        session.UpdatedAt = DateTime.UtcNow;

        if (joinMessage is not null)
            _sseSubscriptions.Notify(session.Id, joinMessage);

        _sseSubscriptions.Notify(session.Id, message);

        if (!string.IsNullOrWhiteSpace(incoming.AuthorId) && !alreadyEnriched)
        {
            var capturedMessageId = message.Id;
            var capturedSessionId = session.Id;
            var stoppingToken = _appLifetime.ApplicationStopping;
            _ = Task.Run(async () =>
            {
                try
                {
                    var info = await _bitrixRestClient.FetchOperatorInfoAsync(incoming.AuthorId, stoppingToken);
                    if (info.FirstName is not null || info.AvatarUrl is not null)
                    {
                        await using var bgDb = _dbContextFactory.CreateDbContext();
                        var bgMessage =
                            await bgDb.LiveChatMessages.FindAsync(new object[] { capturedMessageId }, stoppingToken);
                        if (bgMessage is not null)
                        {
                            bgMessage.SenderName = info.FirstName ?? bgMessage.SenderName;
                            bgMessage.OperatorAvatarUrl = info.AvatarUrl;
                            await bgDb.SaveChangesAsync(stoppingToken);

                            _sseSubscriptions.Notify(capturedSessionId, bgMessage);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    //
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enrich operator info for user {AuthorId}", incoming.AuthorId);
                }
            }, stoppingToken);
        }

        return message;
    }

    public Task SendDeliveryStatusAsync(string messageId, string connectorChatId, CancellationToken ct = default)
    {
        return _bitrixRestClient.SendDeliveryStatusAsync(messageId, connectorChatId, ct);
    }

    public Task<List<LiveChatMessageEntity>> GetMessagesAsync(Guid sessionId, DateTime? after = null,
        CancellationToken ct = default)
    {
        return _messageRepo.GetBySessionAsync(sessionId, after, ct);
    }

    public Task<LiveChatSessionEntity?> GetActiveSessionAsync(string reservationToken, CancellationToken ct = default)
    {
        return _sessionRepo.GetActiveByReservationTokenAsync(reservationToken, ct);
    }

    public Task<string?> GetLandingPreviewUrlAsync(string url, CancellationToken ct = default)
    {
        return _bitrixRestClient.GetLandingPreviewUrlAsync(url, ct);
    }

    private static LiveChatCrmBindingTarget? GetCrmBindingTarget(LiveChatSessionEntity session)
    {
        if (session.DealBitrixId.HasValue)
            return new LiveChatCrmBindingTarget("DEAL", session.DealBitrixId.Value);
        if (session.ClientBitrixId.HasValue)
            return new LiveChatCrmBindingTarget("CONTACT", session.ClientBitrixId.Value);
        return null;
    }

    private async Task<bool> EnsureSessionReservationContextAsync(LiveChatSessionEntity session, CancellationToken ct)
    {
        if (session.IdoReservationId.HasValue &&
            session.DealBitrixId.HasValue)
            return false;

        var updated = false;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var reservationWorkflowService = scope.ServiceProvider.GetRequiredService<IReservationWorkflowService>();
        var reservationWorkflowSyncOps = scope.ServiceProvider.GetRequiredService<IReservationWorkflowSyncOperations>();
        var reservationStore = scope.ServiceProvider.GetRequiredService<IReservationStore>();

        var reservation =
            await reservationWorkflowService.EnsureRentoomReservationByResTokenAsync(session.ReservationToken, ct);
        if (reservation is null)
        {
            _logger.LogWarning("Live chat session {SessionId} could not resolve reservation token {ReservationToken}.",
                session.Id, session.ReservationToken);
            return false;
        }

        if (session.IdoReservationId != reservation.Id)
        {
            session.IdoReservationId = reservation.Id;
            updated = true;
        }

        var record =
            await ResolveReservationRecordAsync(reservationStore, session, reservation.Id, reservation.ResToken, ct);
        if (record is null)
        {
            _logger.LogWarning(
                "Live chat session {SessionId} resolved reservation token {ReservationToken} to IdoReservationId={IdoReservationId}, but no reservation record was found.",
                session.Id,
                session.ReservationToken,
                reservation.Id);
            return updated;
        }

        if (!record.ClientBitrixId.HasValue || !record.DealBitrixId.HasValue)
            record = await reservationWorkflowSyncOps.EnsureBitrixContactAndDealAsync(record);

        if (session.ClientBitrixId != record.ClientBitrixId)
        {
            session.ClientBitrixId = record.ClientBitrixId;
            updated = true;
        }

        if (session.DealBitrixId != record.DealBitrixId)
        {
            session.DealBitrixId = record.DealBitrixId;
            updated = true;
        }

        return updated;
    }

    private static async Task<ReservationRecord?> ResolveReservationRecordAsync(
        IReservationStore reservationStore,
        LiveChatSessionEntity session,
        int idoReservationId,
        string? resolvedReservationToken,
        CancellationToken ct)
    {
        var record = await reservationStore.GetByIdoReservationIdAsync(idoReservationId, ct);
        if (record is not null) return record;

        foreach (var token in new[] { resolvedReservationToken, session.ReservationToken })
        {
            if (!Guid.TryParse(token, out var reservationGuid)) continue;

            record = await reservationStore.GetAsync(reservationGuid, ct);
            if (record is not null) return record;
        }

        return null;
    }

    private static (string? FirstName, string CleanText) ParseBitrixOperatorMessage(string text)
    {
        string? firstName = null;
        var remaining = text;

        var nameMatch = NameHeaderRx.Match(text);

        if (nameMatch.Success)
        {
            var fullName = nameMatch.Groups[1].Value.Trim();
            firstName = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            remaining = text[(nameMatch.Index + nameMatch.Length)..];
        }

        remaining = LeadingBrRx.Replace(remaining, "");

        return (firstName, remaining.Trim());
    }

    public IReadOnlyList<LiveChatAttachmentDto> DeserializeAttachments(LiveChatMessageEntity message)
    {
        if (string.IsNullOrWhiteSpace(message.Attachments))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<LiveChatAttachmentDto>>(message.Attachments, _jsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private IReadOnlyList<LiveChatAttachmentDto> FilterAttachments(
        IReadOnlyList<LiveChatAttachmentDto>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return [];

        var filtered = new List<LiveChatAttachmentDto>(attachments.Count);
        foreach (var att in attachments)
        {
            var preview = IsHttpsUrl(att.UrlPreview) ? att.UrlPreview : null;
            var download = IsHttpsUrl(att.UrlDownload) ? att.UrlDownload : null;

            if (preview is null && download is null)
            {
                _logger.LogWarning(
                    "Attachment '{Name}' skipped: no valid HTTPS URL found (preview={Preview}, download={Download}).",
                    att.Name,
                    att.UrlPreview,
                    att.UrlDownload);
                continue;
            }

            filtered.Add(att with { UrlPreview = preview, UrlDownload = download });
        }

        return filtered;
    }

    private static bool IsHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
    }
}