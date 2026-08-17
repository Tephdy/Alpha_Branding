using Alpha.Branding.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Windows.Media.Imaging;

namespace Alpha.Branding.Services;

public abstract record ImageBatchItem
{
    public sealed record Landscape(string FilePath) : ImageBatchItem;
    public sealed record PortraitPair(string LeftFilePath, string RightFilePath) : ImageBatchItem;
    public sealed record LonePortrait(string FilePath) : ImageBatchItem;
}

public sealed class ImageProcessingService
{
    public const int TargetWidth = 1200;
    public const int TargetHeight = 1000;
    public const int HalfWidth = 600;

    public static async Task<bool> IsPortraitAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(filePath);
        var info = await Image.IdentifyAsync(stream, cancellationToken);
        if (info == null) return false;

        var width = info.Width;
        var height = info.Height;

        if (info.Metadata?.ExifProfile != null && info.Metadata.ExifProfile.TryGetValue(ExifTag.Orientation, out var orientationValue))
        {
            if (orientationValue.Value is ushort val && val is 5 or 6 or 7 or 8)
            {
                (width, height) = (height, width);
            }
        }

        return height > width;
    }

    public static async Task<IReadOnlyList<ImageBatchItem>> PlanBatchAsync(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        if (filePaths.Count == 0) return Array.Empty<ImageBatchItem>();

        var orientations = new (string Path, bool IsPortrait)[filePaths.Count];
        for (var i = 0; i < filePaths.Count; i++)
        {
            var path = filePaths[i];
            var isPortrait = false;
            try
            {
                isPortrait = await IsPortraitAsync(path, cancellationToken);
            }
            catch
            {
                // Fallback to landscape if orientation check fails
            }
            orientations[i] = (path, isPortrait);
        }

        var items = new List<ImageBatchItem>();
        var consumed = new bool[filePaths.Count];

        for (var i = 0; i < filePaths.Count; i++)
        {
            if (consumed[i]) continue;

            var (path, isPortrait) = orientations[i];
            if (!isPortrait)
            {
                items.Add(new ImageBatchItem.Landscape(path));
                consumed[i] = true;
            }
            else
            {
                var pairIndex = -1;
                for (var j = i + 1; j < filePaths.Count; j++)
                {
                    if (!consumed[j] && orientations[j].IsPortrait)
                    {
                        pairIndex = j;
                        break;
                    }
                }

                if (pairIndex != -1)
                {
                    items.Add(new ImageBatchItem.PortraitPair(path, orientations[pairIndex].Path));
                    consumed[i] = true;
                    consumed[pairIndex] = true;
                }
                else
                {
                    items.Add(new ImageBatchItem.LonePortrait(path));
                    consumed[i] = true;
                }
            }
        }

        return items;
    }

    public async Task<BrandedImage> ProcessAsync(string inputPath, string overlayPath, string? prefix, int index, int total, CancellationToken cancellationToken = default)
    {
        var isPortrait = false;
        try
        {
            isPortrait = await IsPortraitAsync(inputPath, cancellationToken);
        }
        catch
        {
            // Fallback to landscape processing if detection fails
        }

        if (isPortrait)
        {
            return await ProcessLonePortraitAsync(inputPath, overlayPath, prefix, index, total, cancellationToken);
        }

        return await ProcessLandscapeAsync(inputPath, overlayPath, prefix, index, total, cancellationToken);
    }

    public async Task<BrandedImage> ProcessLandscapeAsync(string inputPath, string overlayPath, string? prefix, int index, int total, CancellationToken cancellationToken = default)
    {
        await using var input = File.OpenRead(inputPath);
        await using var overlay = File.OpenRead(overlayPath);
        using var photo = await Image.LoadAsync<Rgba32>(input, cancellationToken);
        using var frame = await Image.LoadAsync<Rgba32>(overlay, cancellationToken);

        photo.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(TargetWidth, TargetHeight), Mode = ResizeMode.Stretch }));
        frame.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(TargetWidth, TargetHeight), Mode = ResizeMode.Stretch }));
        photo.Mutate(context => context.DrawImage(frame, new Point(0, 0), 1f));

        return await CreateBrandedImageAsync(photo, prefix, index, total, cancellationToken);
    }

    public async Task<BrandedImage> ProcessPortraitPairAsync(string leftPath, string rightPath, string overlayPath, string? prefix, int index, int total, CancellationToken cancellationToken = default)
    {
        await using var leftStream = File.OpenRead(leftPath);
        await using var rightStream = File.OpenRead(rightPath);
        await using var overlayStream = File.OpenRead(overlayPath);

        using var leftPhoto = await Image.LoadAsync<Rgba32>(leftStream, cancellationToken);
        using var rightPhoto = await Image.LoadAsync<Rgba32>(rightStream, cancellationToken);
        using var frame = await Image.LoadAsync<Rgba32>(overlayStream, cancellationToken);

        leftPhoto.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(HalfWidth, TargetHeight), Mode = ResizeMode.Crop }));
        rightPhoto.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(HalfWidth, TargetHeight), Mode = ResizeMode.Crop }));
        frame.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(TargetWidth, TargetHeight), Mode = ResizeMode.Stretch }));

        using var canvas = new Image<Rgba32>(TargetWidth, TargetHeight);
        canvas.Mutate(context =>
        {
            context.DrawImage(leftPhoto, new Point(0, 0), 1f);
            context.DrawImage(rightPhoto, new Point(HalfWidth, 0), 1f);
            context.DrawImage(frame, new Point(0, 0), 1f);
        });

        return await CreateBrandedImageAsync(canvas, prefix, index, total, cancellationToken);
    }

    public async Task<BrandedImage> ProcessLonePortraitAsync(string inputPath, string overlayPath, string? prefix, int index, int total, CancellationToken cancellationToken = default)
    {
        await using var input = File.OpenRead(inputPath);
        await using var overlay = File.OpenRead(overlayPath);

        using var photo = await Image.LoadAsync<Rgba32>(input, cancellationToken);
        using var frame = await Image.LoadAsync<Rgba32>(overlay, cancellationToken);

        photo.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(HalfWidth, TargetHeight), Mode = ResizeMode.Crop }));
        frame.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(TargetWidth, TargetHeight), Mode = ResizeMode.Stretch }));

        // Center lone portrait photo on dark surface background (#121212)
        using var canvas = new Image<Rgba32>(TargetWidth, TargetHeight, new Rgba32(18, 18, 18, 255));
        canvas.Mutate(context =>
        {
            var offsetX = (TargetWidth - HalfWidth) / 2;
            context.DrawImage(photo, new Point(offsetX, 0), 1f);
            context.DrawImage(frame, new Point(0, 0), 1f);
        });

        return await CreateBrandedImageAsync(canvas, prefix, index, total, cancellationToken);
    }

    public async Task<BrandedImage> ProcessBatchItemAsync(ImageBatchItem item, string overlayPath, string? prefix, int index, int total, CancellationToken cancellationToken = default)
    {
        return item switch
        {
            ImageBatchItem.Landscape landscape => await ProcessLandscapeAsync(landscape.FilePath, overlayPath, prefix, index, total, cancellationToken),
            ImageBatchItem.PortraitPair pair => await ProcessPortraitPairAsync(pair.LeftFilePath, pair.RightFilePath, overlayPath, prefix, index, total, cancellationToken),
            ImageBatchItem.LonePortrait lone => await ProcessLonePortraitAsync(lone.FilePath, overlayPath, prefix, index, total, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(item))
        };
    }

    private static async Task<BrandedImage> CreateBrandedImageAsync(Image<Rgba32> image, string? prefix, int index, int total, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 90 }, cancellationToken);
        return new BrandedImage
        {
            FileName = FileNameGenerator.Generate(prefix, index, total),
            ImageBytes = output.ToArray(),
            Preview = CreatePreview(image),
            SequenceIndex = index,
            BatchSize = total
        };
    }

    private static BitmapImage CreatePreview(Image<Rgba32> image)
    {
        using var preview = new MemoryStream();
        image.SaveAsPng(preview);
        preview.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = preview;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
