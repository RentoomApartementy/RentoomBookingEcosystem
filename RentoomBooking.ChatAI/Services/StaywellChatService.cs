using System.Diagnostics;
using Microsoft.Extensions.Options;
using RentoomBooking.ChatAI.Contracts;
using RentoomBooking.ChatAI.Entities;
using RentoomBooking.ChatAI.Exceptions;
using RentoomBooking.ChatAI.Repositories;

namespace RentoomBooking.ChatAI.Services;

public sealed class StaywellChatService : IStaywellChatService
{
    private readonly StaywellChatOptions _options;
    private readonly IStaywellChatClient _chatClient;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IReservationContextProvider _reservationContextProvider;
    private readonly IChatConversationRepository _conversationRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IChatRateLimiter _rateLimiter;

    public StaywellChatService(
        IOptions<StaywellChatOptions> options,
        IStaywellChatClient chatClient,
        IPromptBuilder promptBuilder,
        IReservationContextProvider reservationContextProvider,
        IChatConversationRepository conversationRepository,
        IChatMessageRepository messageRepository,
        IChatRateLimiter rateLimiter)
    {
        _options = options.Value;
        _chatClient = chatClient;
        _promptBuilder = promptBuilder;
        _reservationContextProvider = reservationContextProvider;
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _rateLimiter = rateLimiter;
    }

    public async Task<StaywellChatStreamResult> StreamAsync(
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

        var context = await _reservationContextProvider.GetContextAsync(request.ReservationId,request.ReservationToken, linkedToken);
        if (context is null)
        {
            throw new ChatForbiddenException("Reservation token is invalid or inactive.");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt(context);
        var conversation = await ResolveConversationAsync(request, systemPrompt, linkedToken);

        var historyMessages = await _messageRepository.GetRecentByConversationAsync(conversation.Id, _options.MaxHistoryMessages, linkedToken);

        var historyTurns = historyMessages
            .Where(m => string.Equals(m.Role, ChatRoles.User, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(m.Role, ChatRoles.Assistant, StringComparison.OrdinalIgnoreCase))
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToList();

        var assistantBuilder = new System.Text.StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        TimeSpan? ttfb = null;
        var chunkCount = 0;

        await foreach (var chunk in _chatClient.CompleteStreamingAsync(systemPrompt, historyTurns, request.Message, linkedToken))
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
                ConversationId = conversation.Id,
                Role = ChatRoles.User,
                Content = request.Message.Trim(),
                TokenCount = EstimateTokenCount(request.Message),
                CreatedAt = now
            },
            new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = ChatRoles.Assistant,
                Content = assistantText,
                TokenCount = EstimateTokenCount(assistantText),
                CreatedAt = now.AddMilliseconds(1)
            }
        }, linkedToken);

        await _conversationRepository.TouchAsync(conversation.Id, linkedToken);

        return new StaywellChatStreamResult(
            conversation.Id,
            chunkCount,
            EstimateTokenCount(request.Message),
            EstimateTokenCount(assistantText),
            ttfb,
            stopwatch.Elapsed,
            Completed: true);
    }

    private async Task<ChatConversationEntity> ResolveConversationAsync(ChatRequestDto request, string systemPrompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            var conversation = await _conversationRepository.CreateAsync(request.ReservationToken, cancellationToken);
            await _messageRepository.AddAsync(new ChatMessageEntity
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                Role = ChatRoles.System,
                Content = systemPrompt,
                TokenCount = EstimateTokenCount(systemPrompt),
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            return conversation;
        }

        if (!Guid.TryParse(request.ConversationId, out var conversationId))
        {
            throw new ChatValidationException("ConversationId is invalid.");
        }

        var existing = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (existing is null)
        {
            throw new ChatNotFoundException("Conversation not found.");
        }

        if (!string.Equals(existing.ReservationToken, request.ReservationToken, StringComparison.OrdinalIgnoreCase))
        {
            throw new ChatForbiddenException("Conversation does not belong to provided reservation.");
        }

        if (!string.Equals(existing.Status, ChatConversationStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ChatForbiddenException("Conversation is not active.");
        }

        return existing;
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
}
