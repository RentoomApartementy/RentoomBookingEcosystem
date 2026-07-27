using RentoomBooking.SharedClasses.Services.Embeds;
using RentoomBooking.SharedClasses.Integrations.RentoomApp.SocialMedia.Models;
using RentoomBooking.SharedClasses.Services.Blog;
using Xunit;

namespace SharedClasses.Tests;

public class YoutubeEmbedHelperTests
{
    [Fact]
    public void BuildEmbedUrl_AddsIframeApiAndPreservesConfiguredOptions()
    {
        var url = YoutubeEmbedHelper.BuildEmbedUrl(
            "<iframe src=\"https://www.youtube.com/embed/M7lc1UVf-VE\"></iframe>",
            autoplay: true,
            mute: true,
            controls: false,
            modestBranding: true,
            loop: true);

        Assert.Equal(
            "https://www.youtube.com/embed/M7lc1UVf-VE?enablejsapi=1&autoplay=1&mute=1&controls=0&modestbranding=1&loop=1&playlist=M7lc1UVf-VE",
            url);
    }

    [Theory]
    [InlineData(null, 10)]
    [InlineData(-1, 0)]
    [InlineData(101, 100)]
    [InlineData(35, 35)]
    public void NormalizeVolume_UsesDefaultAndClampsToYouTubeRange(int? volume, int expected)
    {
        Assert.Equal(expected, YoutubeEmbedHelper.NormalizeVolume(volume));
    }

    [Fact]
    public void VolumeModels_DefaultToTenWhenTheBackendDoesNotProvideAValue()
    {
        Assert.Equal(10, new ApartmentSocialMediaDTO().YouTubeVolume);
        Assert.Equal(10, new ApartmentItemSocialMedia().YouTubeVolume);
        Assert.Equal(10, new BlogBlock().YouTubeVolume);
    }
}
