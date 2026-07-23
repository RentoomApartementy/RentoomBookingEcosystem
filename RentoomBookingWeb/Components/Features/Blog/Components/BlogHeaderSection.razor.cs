using Microsoft.AspNetCore.Components;
using RentoomBooking.SharedClasses.Services.Blog;

namespace RentoomBookingWeb.Components.Features.Blog.Components;

public partial class BlogHeaderSection : ComponentBase, IDisposable
{
    [Inject] 
    protected RentoomBookingWeb.Services.Localization.IRouteLocalizationService RouteService { get; set; } = default!;

    [Inject] 
    internal Microsoft.Extensions.Localization.IStringLocalizer<RentoomBookingWeb.Blog> Localizer { get; set; } = default!;

    [Parameter]
    public BlogPostListItem? Item { get; set; }

    [Parameter]
    public IReadOnlyList<BlogPostListItem>? Items { get; set; }

    [Parameter]
    public int MaxPosts { get; set; } = 10;

    [Parameter]
    public int AutoplayIntervalSeconds { get; set; } = 6;

    [Parameter]
    public bool ShowReadMoreButton { get; set; } = true;

    [Parameter]
    public bool UseH1ForTitle { get; set; } = true;

    private IReadOnlyList<BlogPostListItem> _slides = Array.Empty<BlogPostListItem>();
    private string? _loadedSignature;
    private int _currentIndex;
    private System.Threading.Timer? _timer;
    private int _timerGeneration;
    private bool _hasRendered;
    private bool _isHovered;
    private bool _disposed;

    protected override void OnParametersSet()
    {
        var nextSlides = Items is not null
            ? Items.Take(Math.Max(0, MaxPosts)).ToArray()
            : Item is not null
                ? new[] { Item }
                : Array.Empty<BlogPostListItem>();
        var signature = string.Join(',', nextSlides.Select(slide => slide.PublicId));

        if (signature == _loadedSignature)
        {
            return;
        }

        _loadedSignature = signature;
        _slides = nextSlides;
        _currentIndex = 0;

        if (_hasRendered)
        {
            RestartTimer();
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _hasRendered = true;
        StartTimer();
    }

    private void HandleMouseEnter()
    {
        _isHovered = true;
        StopTimer();
    }

    private void HandleMouseLeave()
    {
        _isHovered = false;
        StartTimer();
    }

    private void GoToSlide(int index)
    {
        if (index < 0 || index >= _slides.Count)
        {
            return;
        }

        _currentIndex = index;
        RestartTimer();
    }

    private void StartTimer()
    {
        if (_disposed || _isHovered || _slides.Count <= 1)
        {
            return;
        }

        StopTimer();
        var interval = TimeSpan.FromSeconds(Math.Max(1, AutoplayIntervalSeconds));
        var timerGeneration = ++_timerGeneration;
        _timer = new System.Threading.Timer(_ => _ = AdvanceAsync(timerGeneration), null, interval, interval);
    }

    private async Task AdvanceAsync(int timerGeneration)
    {
        try
        {
            await InvokeAsync(() =>
            {
                if (_disposed || timerGeneration != _timerGeneration || _slides.Count <= 1)
                {
                    return;
                }

                _currentIndex = (_currentIndex + 1) % _slides.Count;
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // The component was disposed while the timer callback was queued.
        }
    }

    private void RestartTimer()
    {
        StopTimer();
        StartTimer();
    }

    private void StopTimer()
    {
        _timerGeneration++;
        _timer?.Dispose();
        _timer = null;
    }

    private string BuildPostUrl(Guid publicId, string slug) 
        => $"{RouteService.GetLocalizedUrl("BlogPost")}/{publicId:D}/{slug}";

    private string GetReadTimeText(BlogPostListItem item)
    {
        // Estimate read time (roughly 200 chars per minute, min 3 minutes)
        var charCount = (item.Excerpt?.Length ?? 0) + item.Title.Length + 300;
        var minutes = Math.Max(3, (int)Math.Ceiling(charCount / 200.0));
        return string.Format(Localizer["ReadTimeFormat"], minutes);
    }

    public void Dispose()
    {
        _disposed = true;
        StopTimer();
    }
}
