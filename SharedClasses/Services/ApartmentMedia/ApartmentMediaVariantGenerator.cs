using Microsoft.Extensions.Options;
using RentoomBooking.SharedClasses.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RentoomBooking.SharedClasses.Services.ApartmentMedia
{
    public interface IApartmentMediaVariantGenerator
    {
        Task<ApartmentMediaVariantResult> CreateCardVariantAsync(Stream originalContent, CancellationToken cancellationToken = default);
    }

    public sealed class ApartmentMediaVariantResult
    {
        public required MemoryStream Content { get; init; }
        public required string ContentType { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
    }

    public sealed class ApartmentMediaVariantGenerator : IApartmentMediaVariantGenerator
    {
        private readonly ApartmentMediaVariantsOptions _options;

        public ApartmentMediaVariantGenerator(IOptions<ApartmentMediaVariantsOptions> options)
        {
            _options = options.Value;
        }

        public async Task<ApartmentMediaVariantResult> CreateCardVariantAsync(Stream originalContent, CancellationToken cancellationToken = default)
        {
            if (originalContent == null)
            {
                throw new ArgumentNullException(nameof(originalContent));
            }

            if (originalContent.CanSeek)
            {
                originalContent.Position = 0;
            }

            using var image = await Image.LoadAsync(originalContent, cancellationToken);
            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;

            if (image.Width > _options.CardMaxWidth || image.Height > _options.CardMaxHeight)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(_options.CardMaxWidth, _options.CardMaxHeight)
                }));
            }

            var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, new WebpEncoder
            {
                Quality = _options.CardWebpQuality
            }, cancellationToken);

            output.Position = 0;

            return new ApartmentMediaVariantResult
            {
                Content = output,
                ContentType = "image/webp",
                Width = image.Width,
                Height = image.Height
            };
        }
    }
}
