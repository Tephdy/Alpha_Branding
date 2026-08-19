using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.IO;
using System.Linq;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Alpha.Branding.Tests;

public class UiAutomationTests
{
    private static string GetAppExecutablePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Alpha.Branding.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "src", "Alpha.Branding", "bin", "Release", "net8.0-windows", "Alpha.Branding.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "src", "Alpha.Branding", "bin", "Debug", "net8.0-windows", "Alpha.Branding.exe")
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full)) return full;
        }

        throw new FileNotFoundException("Alpha.Branding.exe was not found in build outputs.");
    }

    private static Window GetAppMainWindow(Application app, UIA3Automation automation)
    {
        var result = Retry.WhileNull(() =>
        {
            try
            {
                var win = app.GetMainWindow(automation);
                if (win != null && win.Title.Contains("Alpha Premier")) return win;

                var desktop = automation.GetDesktop();
                var allWins = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                var match = allWins.FirstOrDefault(w => w.Properties.ProcessId.Value == app.ProcessId && w.Name.Contains("Alpha Premier"));
                return match?.AsWindow();
            }
            catch
            {
                return null;
            }
        }, TimeSpan.FromSeconds(10)).Result;

        return result ?? throw new InvalidOperationException("Failed to acquire application main window.");
    }

    private static (string Landscape, string Portrait1, string Portrait2) CreateTestImages(string tempDir)
    {
        var landscape = Path.Combine(tempDir, "L1.png");
        var p1 = Path.Combine(tempDir, "P1.png");
        var p2 = Path.Combine(tempDir, "P2.png");

        using (var imgL = new Image<Rgba32>(1600, 1000))
            imgL.SaveAsPng(landscape);

        using (var imgP1 = new Image<Rgba32>(800, 1200))
            imgP1.SaveAsPng(p1);

        using (var imgP2 = new Image<Rgba32>(800, 1200))
            imgP2.SaveAsPng(p2);

        return (landscape, p1, p2);
    }

    [Fact]
    public void AppLaunchesAndPresentsEmptyStateWithAccessibleAutomationIds()
    {
        var appPath = GetAppExecutablePath();
        using var automation = new UIA3Automation();
        var app = Application.Launch(appPath);

        try
        {
            var window = GetAppMainWindow(app, automation);
            Assert.NotNull(window);
            Assert.Contains("Alpha Premier", window.Title);

            var selectBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("SelectPhotosButton"));
            Assert.NotNull(selectBtn);

            var prefixBox = window.FindFirstDescendant(cf => cf.ByAutomationId("PrefixTextBox"))?.AsTextBox();
            Assert.NotNull(prefixBox);
            Assert.Equal("AlphaPremier_Photo", prefixBox.Text);

            var patternPreview = window.FindFirstDescendant(cf => cf.ByAutomationId("PatternPreviewTextBlock"));
            Assert.NotNull(patternPreview);
            Assert.Equal("AlphaPremier_Photo_01.jpg", patternPreview.Name);

            var applyBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ApplyBrandingButton"));
            Assert.NotNull(applyBtn);

            var exportBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ExportZipButton"));
            Assert.NotNull(exportBtn);

            var summaryText = window.FindFirstDescendant(cf => cf.ByAutomationId("SelectionSummaryTextBlock"));
            Assert.NotNull(summaryText);
            Assert.Equal("No photos selected", summaryText.Name);

            var emptyBannerBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("EmptyStateSelectButton"));
            Assert.NotNull(emptyBannerBtn);
        }
        finally
        {
            try { app.Close(); } catch { }
            try { if (!app.HasExited) app.Kill(); } catch { }
        }
    }

    [Fact]
    public void PrefixInputDynamicallyUpdatesPatternPreview()
    {
        var appPath = GetAppExecutablePath();
        using var automation = new UIA3Automation();
        var app = Application.Launch(appPath);

        try
        {
            var window = GetAppMainWindow(app, automation);
            Assert.NotNull(window);

            var prefixBox = window.FindFirstDescendant(cf => cf.ByAutomationId("PrefixTextBox"))?.AsTextBox();
            Assert.NotNull(prefixBox);

            prefixBox.Text = "Metro_Manila_Condo";
            Thread.Sleep(200);

            var patternPreview = window.FindFirstDescendant(cf => cf.ByAutomationId("PatternPreviewTextBlock"));
            Assert.NotNull(patternPreview);
            Assert.Equal("Metro_Manila_Condo_01.jpg", patternPreview.Name);
        }
        finally
        {
            try { app.Close(); } catch { }
            try { if (!app.HasExited) app.Kill(); } catch { }
        }
    }

    [Fact]
    public void LandscapePhotoProcessingProducesBrandedResultCardWithModalPreview()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var (landscape, _, _) = CreateTestImages(tempDir.FullName);
            var appPath = GetAppExecutablePath();
            using var automation = new UIA3Automation();
            var app = Application.Launch(appPath, $"\"{landscape}\"");

            try
            {
                var window = GetAppMainWindow(app, automation);
                Assert.NotNull(window);

                // Verify selection
                var summaryText = Retry.WhileNull(
                    () => window.FindFirstDescendant(cf => cf.ByAutomationId("SelectionSummaryTextBlock")),
                    TimeSpan.FromSeconds(5)).Result;
                Assert.NotNull(summaryText);
                Assert.Equal("1 photo(s) selected", summaryText.Name);

                // Click Apply Branding
                var applyBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ApplyBrandingButton"))?.AsButton();
                Assert.NotNull(applyBtn);
                applyBtn.Invoke();

                // Wait for status text to report completion
                var statusText = Retry.WhileFalse(
                    () => window.FindFirstDescendant(cf => cf.ByAutomationId("StatusTextBlock"))?.Name?.Contains("Completed") == true,
                    TimeSpan.FromSeconds(8));
                Assert.True(statusText.Success, "Processing should complete and update status text.");

                // Check card and buttons
                var cardName = window.FindFirstDescendant(cf => cf.ByAutomationId("ResultFileNameTextBlock"));
                Assert.NotNull(cardName);
                Assert.Equal("AlphaPremier_Photo_01.jpg", cardName.Name);

                var previewBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("PreviewButton"))?.AsButton();
                Assert.NotNull(previewBtn);

                var saveBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("SaveButton"))?.AsButton();
                Assert.NotNull(saveBtn);

                // Test live rename (INotifyPropertyChanged)
                var prefixBox = window.FindFirstDescendant(cf => cf.ByAutomationId("PrefixTextBox"))?.AsTextBox();
                Assert.NotNull(prefixBox);
                prefixBox.Text = "Villa_Alabang";
                Thread.Sleep(200);

                var updatedCardName = window.FindFirstDescendant(cf => cf.ByAutomationId("ResultFileNameTextBlock"));
                Assert.NotNull(updatedCardName);
                Assert.Equal("Villa_Alabang_01.jpg", updatedCardName.Name);

                // Open Preview Modal
                previewBtn.Invoke();

                var modal = Retry.WhileNull(
                    () => window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Window)),
                    TimeSpan.FromSeconds(5)).Result;
                Assert.NotNull(modal);
                Assert.Contains("Photo Preview", modal.Name);

                var modalPos = Retry.WhileNull(
                    () => modal.FindFirstDescendant(cf => cf.ByAutomationId("PreviewPositionBadgeTextBlock")),
                    TimeSpan.FromSeconds(3)).Result;
                Assert.NotNull(modalPos);
                Assert.Equal("1 of 1", modalPos.Name);

                var prevNavBtn = modal.FindFirstDescendant(cf => cf.ByAutomationId("PreviewPreviousButton"));
                Assert.NotNull(prevNavBtn);

                var nextNavBtn = modal.FindFirstDescendant(cf => cf.ByAutomationId("PreviewNextButton"));
                Assert.NotNull(nextNavBtn);

                // Close modal
                modal.AsWindow().Close();
                Thread.Sleep(300);
            }
            finally
            {
                try { app.Close(); } catch { }
                try { if (!app.HasExited) app.Kill(); } catch { }
            }
        }
        finally
        {
            tempDir.Delete(true);
        }
    }

    [Fact]
    public void PortraitPairDetectionProducesSinglePairedOutputInUI()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var (_, p1, p2) = CreateTestImages(tempDir.FullName);
            var appPath = GetAppExecutablePath();
            using var automation = new UIA3Automation();
            var app = Application.Launch(appPath, $"\"{p1}\" \"{p2}\"");

            try
            {
                var window = GetAppMainWindow(app, automation);
                Assert.NotNull(window);

                var summaryText = Retry.WhileNull(
                    () => window.FindFirstDescendant(cf => cf.ByAutomationId("SelectionSummaryTextBlock")),
                    TimeSpan.FromSeconds(5)).Result;
                Assert.NotNull(summaryText);
                Assert.Equal("2 photo(s) selected", summaryText.Name);

                var applyBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("ApplyBrandingButton"))?.AsButton();
                Assert.NotNull(applyBtn);
                applyBtn.Invoke();

                var statusCompleted = Retry.WhileFalse(
                    () => window.FindFirstDescendant(cf => cf.ByAutomationId("StatusTextBlock"))?.Name?.Contains("Completed 1 image(s).") == true,
                    TimeSpan.FromSeconds(8));
                Assert.True(statusCompleted.Success, "2 portraits must pair into 1 branded landscape output.");
            }
            finally
            {
                try { app.Close(); } catch { }
                try { if (!app.HasExited) app.Kill(); } catch { }
            }
        }
        finally
        {
            tempDir.Delete(true);
        }
    }
}
