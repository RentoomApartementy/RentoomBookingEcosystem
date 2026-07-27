using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using RentoomBooking.SharedClasses.Services.Blog;
using RentoomBookingWeb.Helpers;
using RentoomBookingWeb.Services.Localization;

namespace RentoomBookingWeb.Components.Features.Blog.Pages;

public partial class BlogListPage : ComponentBase, IAsyncDisposable
{
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Inject] public ILogger<BlogListPage> Logger { get; set; } = default!;
    [Inject] public PersistentComponentState ApplicationState { get; set; } = default!;
    [Inject] public IBlogContentReader BlogContentReader { get; set; } = default!;
    [Inject] internal IStringLocalizer<RentoomBookingWeb.Blog> Localizer { get; set; } = default!;
    [Inject] public IRouteLocalizationService RouteService { get; set; } = default!;
    [Inject] public NavigationManager NavManager { get; set; } = default!;

    private const int PageSize = 12;

    protected readonly List<BlogPostListItem> Items = new();
    protected readonly List<BlogCategorySummary> Categories = new();
    protected bool IsLoading;
    protected bool HasMore = true;
    protected string? NextCursor;
    protected string? Error;

    // Route-bound category slug, e.g. "aktualnosci" from /blog/aktualnosci. Null on the unfiltered /blog list.
    [Parameter] public string? Category { get; set; }

    private DotNetObjectReference<BlogListPage>? _objRef;
    private IJSObjectReference? _jsModule;
    private PersistingComponentStateSubscription _subscription;
    private readonly CancellationTokenSource _cts = new();
    private bool _interactive;
    private bool _disposed;
    private string? _loadedCategory;
    private bool _hasInitialized;
    private string BuildPostUrl(string? category, string slug) => BlogUrlBuilder.BuildPostUrl(RouteService, category, slug);

    // Category is free text on the post, not a fixed config list - the display name is simply
    // whatever the currently loaded posts carry for this slug (they all share the same category).
    protected string? CategoryDisplayName => Items.FirstOrDefault()?.Category;

    protected string BuildCategoryListUrl(string? categorySlug) =>
        string.IsNullOrWhiteSpace(categorySlug)
            ? RouteService.GetLocalizedUrl("BlogList")
            : BlogUrlBuilder.BuildCategoryListUrl(RouteService, categorySlug);

    protected bool CategoryNotFound => !string.IsNullOrWhiteSpace(Category) && !IsLoading && string.IsNullOrWhiteSpace(Error) && Items.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        _subscription = ApplicationState.RegisterOnPersisting(PersistState);

        if (ApplicationState.TryTakeFromJson<BlogState>("blog_state", out var restoredState) && restoredState is not null)
        {
            Items.AddRange(restoredState.Items);
            Categories.AddRange(restoredState.Categories);
            NextCursor = restoredState.NextCursor;
            HasMore = restoredState.HasMore;
        }
        else
        {
            await Task.WhenAll(LoadNextPageAsync(_cts.Token), LoadCategoriesAsync(_cts.Token));
        }

        _loadedCategory = Category;
        _hasInitialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!_hasInitialized || string.Equals(Category, _loadedCategory, StringComparison.Ordinal))
        {
            return;
        }

        // Category changed via in-app navigation (e.g. clicking a different category link) while this
        // page instance is reused - reset paging state and reload from scratch for the new filter.
        Items.Clear();
        NextCursor = null;
        HasMore = true;
        Error = null;
        _loadedCategory = Category;

        await LoadNextPageAsync(_cts.Token);
    }

    private Task PersistState()
    {
        ApplicationState.PersistAsJson("blog_state", new BlogState
        {
            Items = Items,
            Categories = Categories,
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
                Category,
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

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await BlogContentReader.GetPublishedCategorySummariesAsync(
                System.Globalization.CultureInfo.CurrentUICulture.Name,
                cancellationToken);
            Categories.Clear();
            Categories.AddRange(categories);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load blog categories.");
        }
    }

    protected string GetCategoryLinkClass(string? categorySlug)
    {
        var isActive = string.IsNullOrWhiteSpace(categorySlug)
            ? string.IsNullOrWhiteSpace(Category)
            : string.Equals(Category, categorySlug, StringComparison.OrdinalIgnoreCase);
        return isActive ? "blog-categories__item blog-categories__item--active" : "blog-categories__item";
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
        public List<BlogCategorySummary> Categories { get; set; } = new();
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }
}
