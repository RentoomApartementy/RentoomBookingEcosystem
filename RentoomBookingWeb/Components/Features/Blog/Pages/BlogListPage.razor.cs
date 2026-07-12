using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogListPage : ComponentBase, IAsyncDisposable
{
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public ILogger<BlogListPage> Logger { get; set; } = default!;
    [Inject] public PersistentComponentState ApplicationState { get; set; } = default!;

    private const int PageSize = 12;

    protected readonly List<BlogPostListItem> Items = new();
    protected bool IsLoading;
    protected bool HasMore = true;
    protected string? NextCursor;
    protected string? Error;

    private DotNetObjectReference<BlogListPage>? _objRef;
    private IJSObjectReference? _jsModule;
    private PersistingComponentStateSubscription _subscription;
    private readonly CancellationTokenSource _cts = new();
    private bool _interactive;
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        _subscription = ApplicationState.RegisterOnPersisting(PersistState);

        if (ApplicationState.TryTakeFromJson<BlogState>("blog_state", out var restoredState) && restoredState is not null)
        {
            Items.AddRange(restoredState.Items);
            NextCursor = restoredState.NextCursor;
            HasMore = restoredState.HasMore;
        }
        else
        {
            await LoadNextPageAsync(_cts.Token);
        }
    }

    private Task PersistState()
    {
        ApplicationState.PersistAsJson("blog_state", new BlogState
        {
            Items = Items,
            NextCursor = NextCursor,
            HasMore = HasMore
        });
        return Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _interactive = true;
            _objRef = DotNetObjectReference.Create(this);
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/infiniteScroll.js");
            await _jsModule.InvokeVoidAsync("init", _objRef);
        }
    }

    [JSInvokable]
    public async Task LoadMoreOnScroll()
    {
        if (_disposed || !_interactive || IsLoading || !HasMore)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await LoadNextPageAsync(_cts.Token);
        }
        finally
        {
            IsLoading = false;
        }
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            Error = null;

            var result = await BlogContentReader.GetPublishedPostsFeedAsync(
                System.Globalization.CultureInfo.CurrentUICulture.Name,
                NextCursor,
                PageSize,
                cancellationToken);

            var newItems = result.Items.Where(newItem => !Items.Any(existingItem => existingItem.PublicId == newItem.PublicId));
            Items.AddRange(newItems);
            NextCursor = result.NextCursor;
            HasMore = result.HasMore;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Error = Localizer["CouldNotLoadBlogPosts"];
            Logger.LogError(ex, "Failed to load blog feed.");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _subscription.Dispose();
        _cts.Cancel();
        _cts.Dispose();

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("unregister");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Failed to dispose blog list JS module.");
            }
        }

        _objRef?.Dispose();
    }

    private class BlogState
    {
        public List<BlogPostListItem> Items { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
