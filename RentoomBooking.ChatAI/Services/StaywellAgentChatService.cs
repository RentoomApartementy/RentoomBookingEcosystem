using System.Diagnostics;
using Microsoft.Extensions.Options;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Entities;
using RentoomBooking.ChatAI.Exceptions;
using RentoomBooking.ChatAI.Repositories;

namespace RentoomBooking.ChatAI.Services;

public sealed class StaywellAgentChatService : IStaywellAgentChatService
{
    private readonly StaywellAgentChatOptions _options;
    private readonly IStaywellAgentChatClient _agentChatClient;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IReservationContextProvider _reservationContextProvider;
    private readonly IChatConversationRepository _conversationRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IChatRateLimiter _rateLimiter;

    public StaywellAgentChatService(
        IOptions<StaywellAgentChatOptions> options,
        IStaywellAgentChatClient agentChatClient,
        IPromptBuilder promptBuilder,
        IReservationContextProvider reservationContextProvider,
        IChatConversationRepository conversationRepository,
        IChatMessageRepository messageRepository,
        IChatRateLimiter rateLimiter)
    {
        _options = options.Value;
        _agentChatClient = agentChatClient;
        _promptBuilder = promptBuilder;
        _reservationContextProvider = reservationContextProvider;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
    }

    public async Task<StaywellAgentChatStreamResult> StreamAsync(
        ChatRequestDto request,
        Func<ChatChunkDto, CancellationToken, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.StreamingTimeoutSeconds > 0)
        {
            linkedCts.CancelAfter(TimeSpan.FromSeconds(_options.StreamingTimeoutSeconds));
        }

        var linkedToken = linkedCts.Token;

        ValidateRequest(request);

        if (!_rateLimiter.TryAcquire(request.ReservationId, out var retryAfter))
        {
            throw new ChatRateLimitException("Rate limit exceeded for this reservation token.", retryAfter);
        }

        var context = await _reservationContextProvider.GetContextAsync(request.ReservationId, request.ReservationToken, linkedToken);
        if (context is null)
        {
            throw new ChatForbiddenException("Reservation token is invalid or inactive.");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt(context);
        var foundryConversationId = await ResolveFoundryConversationIdAsync(request.ConversationId, systemPrompt, linkedToken);
        var auditConversation = await ResolveAuditConversationAsync(request.ReservationToken, foundryConversationId, systemPrompt, linkedToken);

        var assistantBuilder = new System.Text.StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? ttfb = null;
        var chunkCount = 0;

        await foreach (var chunk in _agentChatClient.CompleteStreamingAsync(foundryConversationId, request.Message, linkedToken))
        {
            if (ttfb is null)
            {
                ttfb = stopwatch.Elapsed;
            }

            assistantBuilder.Append(chunk);
            chunkCount++;
            await onChunk(new ChatChunkDto(chunk), linkedToken);
        }

        var assistantText = assistantBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            assistantText = "I am sorry, I could not generate a response.";
        }

        var now = DateTime.UtcNow;
        await _messageRepository.AddRangeAsync(new[]
        {
            new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                ConversationId = auditConversation.Id,
                Role = ChatRoles.User,
                Content = request.Message.Trim(),
                TokenCount = EstimateTokenCount(request.Message),
                CreatedAt = now
            },
            new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                ConversationId = auditConversation.Id,
                Role = ChatRoles.Assistant,
                Content = assistantText,
                TokenCount = EstimateTokenCount(assistantText),
                CreatedAt = now.AddMilliseconds(1)
            }
        }, linkedToken);

        await _conversationRepository.TouchAsync(auditConversation.Id, linkedToken);

        return new StaywellAgentChatStreamResult(
            foundryConversationId,
            chunkCount,
            EstimateTokenCount(systemPrompt) + EstimateTokenCount(request.Message),
            EstimateTokenCount(assistantText),
            ttfb,
            stopwatch.Elapsed,
            Completed: true);
    }

    private async Task<string> ResolveFoundryConversationIdAsync(string? conversationId, string systemPrompt, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            return conversationId.Trim();
        }

        return await _agentChatClient.CreateConversationAsync(systemPrompt, cancellationToken);
    }

    private async Task<ChatConversationEntity> ResolveAuditConversationAsync(
        string reservationToken,
        string foundryConversationId,
        string systemPrompt,
        CancellationToken cancellationToken)
    {
        var auditToken = BuildAuditReservationToken(reservationToken, foundryConversationId);
        var existing = await _conversationRepository.GetLatestByReservationTokenAsync(auditToken, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var conversation = await _conversationRepository.CreateAsync(auditToken, cancellationToken);
        await _messageRepository.AddAsync(new ChatMessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = ChatRoles.System,
            Content = $"FoundryConversationId: {foundryConversationId}{Environment.NewLine}{systemPrompt}",
            TokenCount = EstimateTokenCount(systemPrompt),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return conversation;
    }

    private void ValidateRequest(ChatRequestDto request)
    {
        if (request is null)
        {
            throw new ChatValidationException("Request payload is required.");
        }

        if (request.ReservationId <= 0)
        {
            throw new ChatValidationException("ReservationId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ChatValidationException("Message is required.");
        }

        var messageLength = request.Message.Trim().Length;
        if (messageLength > _options.MaxMessageLength)
        {
            throw new ChatValidationException($"Message is too long. Maximum length is {_options.MaxMessageLength} characters.");
        }
    }

    private static int EstimateTokenCount(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Math.Max(1, content.Length / 4);
    }

    private static string BuildAuditReservationToken(string reservationToken, string foundryConversationId)
    {
        return $"{reservationToken}:agent:{foundryConversationId}";
    }
}
