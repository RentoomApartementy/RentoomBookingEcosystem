# StayWell AI Chat Runbook

## Scope
- Backend: Azure Functions Isolated (`Api`) with SSE endpoint `POST /api/staywell/chat/stream`
- Frontend: Blazor WASM (`StayWell`) with streaming UI panel
- Data: PostgreSQL (`chat_conversations`, `chat_messages`)
- Core chat module: `RentoomBooking.ChatAI`

## Required Configuration
Use `StaywellChat` section in Function App settings (or `local.settings.json` for local run):

```json
{
  "StaywellChat": {
    "Endpoint": "<azure-openai-endpoint>",
    "ApiKey": "<azure-openai-api-key>",
    "DeploymentName": "<deployment-name>",
    "MaxMessageLength": 2000,
    "MaxHistoryMessages": 15,
    "MaxRequestsPerMinute": 12,
    "StreamingTimeoutSeconds": 90
  }
}
```

If `StaywellChat` is not configured, fallback uses existing `AzureOpenAi` / `AzureOpenAi_general` sections.

## Database Migration
Apply SQL script:

- `RentoomBooking.ChatAI/Migrations/20260416_add_staywell_chat_tables.sql`

This creates:
- `chat_conversations`
- `chat_messages`
- indexes for reservation lookup and conversation timeline

## Local Run
1. Ensure PostgreSQL connection is configured for `Api`.
2. Ensure Azure OpenAI/Foundry settings are configured.
3. Build projects:
   - `dotnet build Api/RentoomBooking.Api.csproj`
   - `dotnet build StayWell/RentoomBooking.StayWell.csproj`
4. Start API and StayWell locally.
5. Open StayWell reservation flow and launch chat from Header or Contact page.

## Smoke Test Checklist
1. Send first question and verify assistant text appears incrementally (streaming).
2. Send 2-3 follow-up questions and verify context is remembered.
3. Refresh page, reopen chat, send another message and verify context persists (same `conversationId` from local storage).
4. During generation click `Stop` and verify stream ends quickly (no full assistant message persisted).
5. Trigger invalid token and verify API returns `403`.
6. Trigger rate limit and verify API returns `429`.

## Observability
API logs include:
- correlation id
- hashed reservation token
- conversation id
- chunk count
- estimated token usage (prompt/completion)
- TTFB and total duration
- cancel/error events

## Troubleshooting
- No streaming in UI:
  - verify browser streaming enabled in request (`SetBrowserResponseStreamingEnabled(true)`)
  - verify response headers: `text/event-stream`, `no-cache`, `keep-alive`
- Empty/failed responses:
  - verify reservation token is active and resolvable
  - verify AI deployment name and endpoint
- Frequent `429`:
  - adjust `StaywellChat:MaxRequestsPerMinute`
