using Alpha.Branding.Models;
using Alpha.Branding.Services;
using Alpha.Branding.ViewModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
using System.IO.Compression;

namespace Alpha.Branding.Tests;

public class FileNameGeneratorTests
{
    [Fact]
    public void SanitizesControlsSeparatorsAndTrailingPunctuation()
    {
        Assert.Equal("Listing_01.jpg", FileNameGenerator.Generate(" Listing:/\0. ", 0, 10));
        Assert.Equal("Home_100.jpg", FileNameGenerator.Generate("Home", 99, 100));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.txt")]
    [InlineData("Lpt9")]
    public void FallsBackForReservedNames(string prefix) =>
        Assert.Equal("AlphaPremier_Photo", FileNameGenerator.FolderName(prefix));

    [Fact]
    public void CapsLongPrefixAndSanitizesExtension()
    {
        var name = FileNameGenerator.Generate(new string('x', 500), 0, 1, ".JPG!");

        Assert.Equal(120, name.Length);
        Assert.EndsWith("_01.jpg", name);
    }

    [Theory]
    [InlineData("Bahay_Kubo_#1", "Bahay_Kubo_#1_01.jpg")]
    [InlineData("Mandaluyong / Condo * 101?", "Mandaluyong  Condo  101_01.jpg")]
    [InlineData("  Maynila_Proyekto_  ", "Maynila_Proyekto__01.jpg")]
    public void HandlesUnicodeSpecialCharactersAndWhitespace(string input, string expected)
    {
        Assert.Equal(expected, FileNameGenerator.Generate(input, 0, 5));
    }
}

public class UiInitializationTests
{
    [Fact]
    public void CanInstantiateWindowsWithoutException()
    {
        var thread = new System.Threading.Thread(() =>
        {
            if (System.Windows.Application.Current == null)
                _ = new App();
            var window = new MainWindow();
            Assert.NotNull(window);
            var preview = new PreviewWindow(new List<BrandedImage>(), 0);
            Assert.NotNull(preview);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void BrandedImageRaisesPropertyChangedWhenFileNameChanges()
    {
        var item = new BrandedImage
        {
            FileName = "Initial_01.jpg",
            ImageBytes = Array.Empty<byte>(),
            Preview = new System.Windows.Media.Imaging.BitmapImage(),
            SequenceIndex = 0,
            BatchSize = 1
        };

        var fired = false;
        item.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BrandedImage.FileName))
                fired = true;
        };

        item.FileName = "Updated_01.jpg";
        Assert.True(fired, "BrandedImage must raise PropertyChanged for UI data binding when FileName is mutated.");
        Assert.Equal("Updated_01.jpg", item.FileName);
    }

    [Fact]
    public void MainWindowLoadFilesFiltersSupportedExtensionsOnly()
    {
        var thread = new System.Threading.Thread(() =>
        {
            if (System.Windows.Application.Current == null)
                _ = new App();

            var tempImg = Path.GetTempFileName() + ".png";
            var tempTxt = Path.GetTempFileName() + ".txt";
            try
            {
                File.WriteAllText(tempImg, "fake image");
                File.WriteAllText(tempTxt, "text file");

                var window = new MainWindow();
                window.LoadFiles(new[] { tempImg, tempTxt, "non_existent.jpg" });

                var vm = (MainWindowViewModel)window.DataContext;
                Assert.Single(vm.SelectedFiles);
                Assert.Equal(tempImg, vm.SelectedFiles[0]);
            }
            finally
            {
                if (File.Exists(tempImg)) File.Delete(tempImg);
                if (File.Exists(tempTxt)) File.Delete(tempTxt);
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}


public class ImageProcessingTests
{
    [Fact]
    public async Task ProcessingProducesExactJpegDimensionsAndCompositesOverlay()
    {
        var input = Path.GetTempFileName();
        var overlay = Path.GetTempFileName();
        try
        {
            using (var image = new Image<Rgba32>(32, 24, new Rgba32(255, 0, 0, 255)))
                await image.SaveAsPngAsync(input);
            using (var frame = new Image<Rgba32>(8, 8, new Rgba32(0, 0, 255, 255)))
                await frame.SaveAsPngAsync(overlay);

            var result = await new ImageProcessingService().ProcessAsync(input, overlay, "Test", 0, 1);
            using var decoded = Image.Load<Rgba32>(result.ImageBytes);
            var centerPixel = decoded[600, 500];

            Assert.Equal(1200, decoded.Width);
            Assert.Equal(1000, decoded.Height);
            Assert.Equal(JpegFormat.Instance, Image.DetectFormat(result.ImageBytes));
            Assert.True(centerPixel.B > centerPixel.R, "The opaque blue overlay should be visible in the composed output.");
        }
        finally
        {
            File.Delete(input);
            File.Delete(overlay);
        }
    }

    [Fact]
    public async Task DetectsPortraitAndLandscapeCorrectly()
    {
        var portraitFile = Path.GetTempFileName();
        var landscapeFile = Path.GetTempFileName();
        try
        {
            using (var portrait = new Image<Rgba32>(1000, 1500))
                await portrait.SaveAsPngAsync(portraitFile);
            using (var landscape = new Image<Rgba32>(1500, 1000))
                await landscape.SaveAsPngAsync(landscapeFile);

            Assert.True(await ImageProcessingService.IsPortraitAsync(portraitFile));
            Assert.False(await ImageProcessingService.IsPortraitAsync(landscapeFile));
        }
        finally
        {
            File.Delete(portraitFile);
            File.Delete(landscapeFile);
        }
    }

    [Fact]
    public async Task PlanBatchPairsPortraitImagesAndKeepsLandscapeSingle()
    {
        var p1 = Path.GetTempFileName();
        var p2 = Path.GetTempFileName();
        var p3 = Path.GetTempFileName();
        var p4 = Path.GetTempFileName();
        var l1 = Path.GetTempFileName();
        var l2 = Path.GetTempFileName();

        try
        {
            using (var p = new Image<Rgba32>(600, 1000))
            {
                await p.SaveAsPngAsync(p1);
                await p.SaveAsPngAsync(p2);
                await p.SaveAsPngAsync(p3);
                await p.SaveAsPngAsync(p4);
            }
            using (var l = new Image<Rgba32>(1200, 1000))
            {
                await l.SaveAsPngAsync(l1);
                await l.SaveAsPngAsync(l2);
            }

            // Case 1: 2 portraits -> 1 pair
            var plan2P = await ImageProcessingService.PlanBatchAsync(new[] { p1, p2 });
            Assert.Single(plan2P);
            var pair = Assert.IsType<ImageBatchItem.PortraitPair>(plan2P[0]);
            Assert.Equal(p1, pair.LeftFilePath);
            Assert.Equal(p2, pair.RightFilePath);

            // Case 2: 4 portraits -> 2 pairs
            var plan4P = await ImageProcessingService.PlanBatchAsync(new[] { p1, p2, p3, p4 });
            Assert.Equal(2, plan4P.Count);
            Assert.IsType<ImageBatchItem.PortraitPair>(plan4P[0]);
            Assert.IsType<ImageBatchItem.PortraitPair>(plan4P[1]);

            // Case 3: Mixed: L1, P1, L2, P2, P3
            var planMixed = await ImageProcessingService.PlanBatchAsync(new[] { l1, p1, l2, p2, p3 });
            Assert.Equal(4, planMixed.Count);
            Assert.IsType<ImageBatchItem.Landscape>(planMixed[0]);
            var mixedPair = Assert.IsType<ImageBatchItem.PortraitPair>(planMixed[1]);
            Assert.Equal(p1, mixedPair.LeftFilePath);
            Assert.Equal(p2, mixedPair.RightFilePath);
            Assert.IsType<ImageBatchItem.Landscape>(planMixed[2]);
            var lone = Assert.IsType<ImageBatchItem.LonePortrait>(planMixed[3]);
            Assert.Equal(p3, lone.FilePath);
        }
        finally
        {
            File.Delete(p1);
            File.Delete(p2);
            File.Delete(p3);
            File.Delete(p4);
            File.Delete(l1);
            File.Delete(l2);
        }
    }

    [Fact]
    public async Task ProcessPortraitPairCompositesLeftAndRightSideBySide()
    {
        var leftFile = Path.GetTempFileName();
        var rightFile = Path.GetTempFileName();
        var overlayFile = Path.GetTempFileName();

        try
        {
            // Left photo is pure red (255, 0, 0)
            using (var left = new Image<Rgba32>(600, 1000, new Rgba32(255, 0, 0, 255)))
                await left.SaveAsPngAsync(leftFile);

            // Right photo is pure green (0, 255, 0)
            using (var right = new Image<Rgba32>(600, 1000, new Rgba32(0, 255, 0, 255)))
                await right.SaveAsPngAsync(rightFile);

            // Overlay is transparent with a small blue box at top-right
            using (var overlay = new Image<Rgba32>(1200, 1000, new Rgba32(0, 0, 0, 0)))
            {
                overlay[1100, 50] = new Rgba32(0, 0, 255, 255);
                await overlay.SaveAsPngAsync(overlayFile);
            }

            var service = new ImageProcessingService();
            var result = await service.ProcessPortraitPairAsync(leftFile, rightFile, overlayFile, "PairTest", 0, 1);

            Assert.NotNull(result);
            Assert.Equal("PairTest_01.jpg", result.FileName);

            using var decoded = Image.Load<Rgba32>(result.ImageBytes);
            Assert.Equal(1200, decoded.Width);
            Assert.Equal(1000, decoded.Height);

            // Left side pixel (x=200, y=500) should be predominantly Red
            var leftPixel = decoded[200, 500];
            Assert.True(leftPixel.R > 200 && leftPixel.G < 50, "Left side should contain left red image.");

            // Right side pixel (x=900, y=500) should be predominantly Green
            var rightPixel = decoded[900, 500];
            Assert.True(rightPixel.G > 200 && rightPixel.R < 50, "Right side should contain right green image.");
        }
        finally
        {
            File.Delete(leftFile);
            File.Delete(rightFile);
            File.Delete(overlayFile);
        }
    }

    [Fact]
    public async Task ProcessLonePortraitCompositesCentered()
    {
        var loneFile = Path.GetTempFileName();
        var overlayFile = Path.GetTempFileName();

        try
        {
            using (var photo = new Image<Rgba32>(600, 1000, new Rgba32(255, 0, 0, 255)))
                await photo.SaveAsPngAsync(loneFile);
            using (var overlay = new Image<Rgba32>(1200, 1000, new Rgba32(0, 0, 0, 0)))
                await overlay.SaveAsPngAsync(overlayFile);

            var service = new ImageProcessingService();
            var result = await service.ProcessLonePortraitAsync(loneFile, overlayFile, "LoneTest", 0, 1);

            Assert.NotNull(result);
            using var decoded = Image.Load<Rgba32>(result.ImageBytes);
            Assert.Equal(1200, decoded.Width);
            Assert.Equal(1000, decoded.Height);

            // Center pixel (x=600, y=500) should be Red
            var centerPixel = decoded[600, 500];
            Assert.True(centerPixel.R > 200);

            // Left edge pixel (x=50, y=500) should be the dark background
            var darkEdgePixel = decoded[50, 500];
            Assert.True(darkEdgePixel.R < 30 && darkEdgePixel.G < 30 && darkEdgePixel.B < 30);
        }
        finally
        {
            File.Delete(loneFile);
            File.Delete(overlayFile);
        }
    }

    [Fact]
    public async Task ViewModelApplyProcessesPortraitPairsCorrectly()
    {
        var p1 = Path.GetTempFileName();
        var p2 = Path.GetTempFileName();
        var overlay = Path.GetTempFileName();

        try
        {
            using (var portrait = new Image<Rgba32>(600, 1000, new Rgba32(255, 255, 255, 255)))
            {
                await portrait.SaveAsPngAsync(p1);
                await portrait.SaveAsPngAsync(p2);
            }
            using (var frame = new Image<Rgba32>(1200, 1000, new Rgba32(0, 0, 0, 0)))
                await frame.SaveAsPngAsync(overlay);

            var vm = new MainWindowViewModel(new ImageProcessingService())
            {
                SelectedFiles = new[] { p1, p2 },
                Prefix = "Listing"
            };

            await vm.ApplyAsync(overlay);

            // 2 portrait photos should result in 1 paired branded output
            Assert.Single(vm.Results);
            Assert.Equal("Listing_01.jpg", vm.Results[0].FileName);
        }
        finally
        {
            File.Delete(p1);
            File.Delete(p2);
            File.Delete(overlay);
        }
    }
}

public class ZipSafetyTests
{
    [Fact]
    public async Task ZipExportContainsExpectedScopedEntries()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var vm = new MainWindowViewModel(new ImageProcessingService());
            var bytes = new byte[] { 1, 2, 3 };
            vm.Results.Add(new BrandedImage
            {
                FileName = FileNameGenerator.Generate("../unsafe", 0, 1),
                ImageBytes = bytes,
                Preview = new System.Windows.Media.Imaging.BitmapImage()
            });
            vm.Prefix = "../unsafe";
            var path = Path.Combine(directory.FullName, "result.zip");

            await vm.ExportZipAsync(path);

            using var archive = ZipFile.OpenRead(path);
            var entry = Assert.Single(archive.Entries);
            Assert.Equal($"{FileNameGenerator.FolderName(vm.Prefix)}/{vm.Results[0].FileName}", entry.FullName);
            Assert.DoesNotContain("..", entry.FullName);
            Assert.Equal(bytes, await ReadEntryAsync(entry));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry)
    {
        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }
}
