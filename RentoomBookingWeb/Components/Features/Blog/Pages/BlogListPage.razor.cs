using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogListPage : ComponentBase, IAsyncDisposable
{
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public ILogger<BlogListPage> Logger { get; set; } = default!;

    protected readonly List<BlogPostListItem> Items = new();
    protected bool IsLoading;
    protected bool HasMore = true;
    protected string? NextCursor;
    protected string? Error;

    private DotNetObjectReference<BlogListPage>? _objRef;
    private IJSObjectReference? _jsModule;
    private readonly CancellationTokenSource _cts = new();
    private bool _interactive;
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        await LoadNextPageAsync(_cts.Token);
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

        await LoadNextPageAsync(_cts.Token);
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
                12,
                cancellationToken);

            Items.AddRange(result.Items);
            NextCursor = result.NextCursor;
            HasMore = result.HasMore;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Error = "Could not load blog posts.";
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
}
