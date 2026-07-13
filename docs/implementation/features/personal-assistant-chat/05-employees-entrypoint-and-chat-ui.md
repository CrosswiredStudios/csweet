# Phase 5 — Employees entrypoint and chat UI

## Goal

Make the agent cards on the Employees page clickable (for agents that are **not** "Self"), and
build the `Chat.razor` page that opens a conversation and streams the assistant's reply into a
live chat transcript.

## Why this phase matters

This is the part the user actually sees. Everything in Phases 1–4 exists so that this page can
do three simple things: start a conversation, send a message, and render the streamed reply.

## Prerequisites

- Phase 1 endpoints: create conversation, get messages.
- Phase 4 endpoint: the SSE stream.
- Familiarity with the current page and client patterns:
  - [src/CSweet.UI/Pages/Employees.razor](../../../../src/CSweet.UI/Pages/Employees.razor)
  - [src/CSweet.UI/Services/OrganizationApiClient.cs](../../../../src/CSweet.UI/Services/OrganizationApiClient.cs)
  - [src/CSweet.UI/Services/ServiceCollectionExtensions.cs](../../../../src/CSweet.UI/Services/ServiceCollectionExtensions.cs)

## Deliverables

- Clickable agent cards on the Employees page → navigate to the chat route.
- A `ChatApiClient` that starts conversations, loads messages, and reads the SSE stream.
- A `Chat.razor` page under the command center layout with a MudBlazor chat UI.
- Client registration in DI.

## Step-by-step

### 1. Make agent cards clickable

In [src/CSweet.UI/Pages/Employees.razor](../../../../src/CSweet.UI/Pages/Employees.razor), the
directory renders a `MudCard` per employee inside `<section class="employee-directory-grid">`.
Add a click handler that only navigates for agent employees that are not "Self".

Add a helper in the `@code` block:

```csharp
@inject NavigationManager Navigation

private bool IsChattableAgent(OrganizationUserResponse employee) =>
    employee.EmployeeType == 1 // Agent
    && !string.Equals(employee.DisplayName, "Self", StringComparison.OrdinalIgnoreCase);

private void OpenChat(OrganizationUserResponse employee)
{
    if (IsChattableAgent(employee))
    {
        Navigation.NavigateTo($"/organizations/{OrganizationId}/chat/{employee.Id}");
    }
}
```

Update the card markup so agent cards look and behave clickable (add a hover cursor, an
"Open chat" affordance, and the click), while human/"Self" cards stay static:

```razor
<MudCard Elevation="1"
         Class="@($"employee-card {(IsChattableAgent(employee) ? "employee-card-clickable" : string.Empty)}")"
         @onclick="@(() => OpenChat(employee))">
    <MudCardContent>
        <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
            <MudAvatar Color="@(employee.EmployeeType == 1 ? Color.Secondary : Color.Primary)" Variant="Variant.Filled">
                @NodeInitials(employee)
            </MudAvatar>
            <div>
                <MudText Typo="Typo.subtitle1">@employee.DisplayName</MudText>
                <MudText Typo="Typo.body2" Color="Color.Secondary">@EmployeeLabel(employee)</MudText>
            </div>
        </MudStack>
        <dl class="employee-card-details">
            <div>
                <dt>Reports to</dt>
                <dd>@ReportsTo(employee)</dd>
            </div>
            <div>
                <dt>Subordinates</dt>
                <dd>@SubordinateCount(employee)</dd>
            </div>
        </dl>
        @if (IsChattableAgent(employee))
        {
            <MudButton Variant="Variant.Text" Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Chat"
                       OnClick="@(() => OpenChat(employee))"
                       OnClickPreventDefault="true">
                Open chat
            </MudButton>
        }
    </MudCardContent>
</MudCard>
```

Add a small style (in the page's scoped CSS or the shared stylesheet) so clickable cards show a
pointer cursor:

```css
.employee-card-clickable { cursor: pointer; }
.employee-card-clickable:hover { box-shadow: var(--mud-elevation-4); }
```

> Keep the graph SVG nodes non-interactive for now — the directory cards are the entry point.

### 2. Chat API client

Create the interface `src/CSweet.UI/Services/IChatApiClient.cs`:

```csharp
using CSweet.Contracts.Core;

namespace CSweet.UI.Services;

public interface IChatApiClient
{
    Task<ConversationResponse> StartConversationAsync(
        Guid organizationId, Guid agentOrganizationUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid organizationId, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Streams assistant deltas. Each yielded string is a token/chunk to append.</summary>
    IAsyncEnumerable<string> SendMessageAsync(
        Guid organizationId, Guid conversationId, string message, CancellationToken cancellationToken = default);
}
```

Create the implementation `src/CSweet.UI/Services/ChatApiClient.cs`. The interesting part is
reading the SSE stream: request with `HttpCompletionOption.ResponseHeadersRead`, then read the
body line-by-line and parse `data:` lines.

```csharp
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Contracts.Core;

namespace CSweet.UI.Services;

public sealed class ChatApiClient : IChatApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ChatApiClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<ConversationResponse> StartConversationAsync(
        Guid organizationId, Guid agentOrganizationUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/core/organizations/{organizationId}/conversations",
            new StartConversationRequest(agentOrganizationUserId),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ConversationActionResponse>(cancellationToken);
            throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to start conversation.");
        }

        return await response.Content.ReadFromJsonAsync<ConversationResponse>(cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "Empty conversation response.");
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid organizationId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ConversationMessageResponse>>(
            $"api/core/organizations/{organizationId}/conversations/{conversationId}/messages",
            cancellationToken) ?? [];
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        Guid organizationId, Guid conversationId, string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/core/organizations/{organizationId}/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new SendChatMessageRequest(message))
        };

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line["data:".Length..].Trim();
            var chunk = JsonSerializer.Deserialize<StreamChunk>(json, SerializerOptions);
            if (chunk is null)
            {
                continue;
            }

            if (chunk.IsFinal)
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                yield return chunk.Delta;
            }
        }
    }

    private sealed record StreamChunk(int Sequence, string Delta, bool IsFinal);
}
```

Register it in
[src/CSweet.UI/Services/ServiceCollectionExtensions.cs](../../../../src/CSweet.UI/Services/ServiceCollectionExtensions.cs):

```csharp
services.AddScoped<IChatApiClient, ChatApiClient>();
```

> `ChatApiClient` uses the same injected `HttpClient` (base address = API) that the other
> clients use — no special setup. Blazor WASM's `HttpClient` supports response streaming, so
> tokens arrive incrementally rather than all at once.

### 3. The Chat page

Create `src/CSweet.UI/Pages/Chat.razor`. It:

- Loads the agent employee (to show its name) and starts (or is given) a conversation.
- Renders a scrollable transcript of messages.
- Has an input box; on send, appends the user's message, then appends assistant tokens as they
  stream in.

```razor
@page "/organizations/{OrganizationId:guid}/chat/{AgentUserId:guid}"
@layout CommandCenterLayout
@using CSweet.Contracts.Core
@using CSweet.UI.Services
@inject HttpClient Http
@inject IChatApiClient ChatApi
@inject NavigationManager Navigation

<PageTitle>Chat - C-Sweet</PageTitle>

<div class="command-center-shell chat-page">
    <MudPaper Elevation="0" Class="page-header">
        <div>
            <MudText Typo="Typo.overline" Color="Color.Primary">Conversation</MudText>
            <MudText Typo="Typo.h4">@(_agent?.DisplayName ?? "Agent")</MudText>
        </div>
        <MudButton Href="@($"/organizations/{OrganizationId}/employees")"
                   Variant="Variant.Outlined" StartIcon="@Icons.Material.Filled.ArrowBack">
            Employees
        </MudButton>
    </MudPaper>

    @if (!string.IsNullOrWhiteSpace(_errorMessage))
    {
        <MudAlert Severity="Severity.Error" Variant="Variant.Filled">@_errorMessage</MudAlert>
    }

    <MudPaper Elevation="1" Class="surface-panel chat-transcript">
        @foreach (var msg in _messages)
        {
            <div class="chat-bubble @(msg.Role == 0 ? "chat-bubble-user" : "chat-bubble-assistant")">
                <MudText Typo="Typo.body1">@msg.Content</MudText>
            </div>
        }
        @if (_streaming)
        {
            <div class="chat-bubble chat-bubble-assistant">
                <MudText Typo="Typo.body1">@_streamingText</MudText>
                <MudProgressLinear Indeterminate="true" Color="Color.Primary" Class="mt-1" />
            </div>
        }
    </MudPaper>

    <MudPaper Elevation="1" Class="surface-panel chat-input">
        <MudTextField @bind-Value="_draft" Placeholder="Message your assistant..."
                      Variant="Variant.Outlined" Lines="2" Immediate="true"
                      OnKeyDown="OnKeyDown" Disabled="_streaming" />
        <MudButton Color="Color.Primary" Variant="Variant.Filled"
                   OnClick="SendAsync" Disabled="@(_streaming || string.IsNullOrWhiteSpace(_draft))"
                   EndIcon="@Icons.Material.Filled.Send">
            Send
        </MudButton>
    </MudPaper>
</div>

@code {
    [Parameter] public Guid OrganizationId { get; set; }
    [Parameter] public Guid AgentUserId { get; set; }

    private OrganizationUserResponse? _agent;
    private ConversationResponse? _conversation;
    private readonly List<ConversationMessageResponse> _messages = [];
    private string _draft = string.Empty;
    private string _streamingText = string.Empty;
    private bool _streaming;
    private string? _errorMessage;

    protected override async Task OnParametersSetAsync()
    {
        _errorMessage = null;
        try
        {
            _agent = await Http.GetFromJsonAsync<OrganizationUserResponse>(
                $"api/core/organizations/{OrganizationId}/users/{AgentUserId}");

            // No conversation persistence linkage yet: start a fresh conversation for this session.
            _conversation = await ChatApi.StartConversationAsync(OrganizationId, AgentUserId);

            var existing = await ChatApi.GetMessagesAsync(OrganizationId, _conversation.Id);
            _messages.Clear();
            _messages.AddRange(existing);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
    }

    private async Task OnKeyDown(KeyboardEventArgs args)
    {
        // Enter sends; Shift+Enter makes a newline.
        if (args.Key == "Enter" && !args.ShiftKey)
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_streaming || _conversation is null || string.IsNullOrWhiteSpace(_draft))
        {
            return;
        }

        var text = _draft.Trim();
        _draft = string.Empty;

        // Optimistically show the user's message.
        _messages.Add(new ConversationMessageResponse(
            Guid.NewGuid(), _conversation.Id, 0, text, DateTimeOffset.UtcNow));

        _streaming = true;
        _streamingText = string.Empty;
        StateHasChanged();

        try
        {
            await foreach (var delta in ChatApi.SendMessageAsync(OrganizationId, _conversation.Id, text))
            {
                _streamingText += delta;
                StateHasChanged(); // render each token as it arrives
            }

            _messages.Add(new ConversationMessageResponse(
                Guid.NewGuid(), _conversation.Id, 1, _streamingText, DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            _errorMessage = $"The assistant could not respond: {ex.Message}";
        }
        finally
        {
            _streaming = false;
            _streamingText = string.Empty;
            StateHasChanged();
        }
    }
}
```

> **Conversation lifetime.** For this feature we start a fresh conversation each time the page
> opens (persistence stores it, but we do not yet resume a prior one). Resuming the latest
> conversation for an agent is an easy later enhancement: add a `GET .../conversations?agentId=`
> lookup and, if found, load its messages instead of starting a new one.

### 4. Minimal styling

Add styles for the transcript and bubbles (scoped `Chat.razor.css` or the shared stylesheet):

```css
.chat-transcript { display: flex; flex-direction: column; gap: 8px; max-height: 60vh; overflow-y: auto; padding: 12px; }
.chat-bubble { padding: 8px 12px; border-radius: 12px; max-width: 75%; }
.chat-bubble-user { align-self: flex-end; background: var(--mud-palette-primary-hover); }
.chat-bubble-assistant { align-self: flex-start; background: var(--mud-palette-surface); border: 1px solid var(--mud-palette-lines-default); }
.chat-input { display: flex; gap: 8px; align-items: flex-end; margin-top: 8px; }
```

Auto-scrolling the transcript to the bottom as tokens arrive is a nice touch (via a small
`IJSRuntime` scroll call after `StateHasChanged`), but is optional.

## Testing

- **Manual (primary):** run the app, open an org's Employees page, confirm the "Self" card is
  not clickable and the Personal Assistant card is. Click it, send a message, and watch the
  reply stream in. Refresh mid-thought and confirm the app does not crash.
- **bUnit component tests (optional):** if the project already has bUnit, assert that
  `IsChattableAgent` gates the click and that streamed deltas from a fake `IChatApiClient`
  accumulate into an assistant bubble.

## Acceptance criteria

- [ ] The "Self" card and human cards are not clickable; agent cards are, and show an
      "Open chat" affordance.
- [ ] Clicking an agent card navigates to `/organizations/{orgId}/chat/{agentUserId}`.
- [ ] The chat page shows the agent's name and a working message input.
- [ ] Sending a message shows the user bubble immediately and streams the assistant reply
      token-by-token into an assistant bubble.
- [ ] The input is disabled while a reply streams (prevents overlapping sends).
- [ ] Errors (no provider configured, agent failure) render a readable alert.

## Definition of done

A user can go Employees → click the Personal Assistant → type a message → watch a streamed
reply appear, entirely through the UI.
