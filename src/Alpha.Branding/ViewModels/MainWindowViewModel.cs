using Alpha.Branding.Models;
using Alpha.Branding.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Alpha.Branding.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ImageProcessingService _processor;
    private string _prefix = FileNameGenerator.DefaultPrefix;
    private bool _isBusy;
    private string _status = "Select property photos to begin.";
    private double _progress;
    private IReadOnlyList<string> _selectedFiles = [];

    public MainWindowViewModel(ImageProcessingService processor) => _processor = processor;

    public ObservableCollection<BrandedImage> Results { get; } = [];

    public string Prefix
    {
        get => _prefix;
        set
        {
            _prefix = value ?? string.Empty;
            RenameResults();
            OnPropertyChanged();
            OnPropertyChanged(nameof(PatternPreview));
        }
    }

    public string PatternPreview => FileNameGenerator.Generate(Prefix, 0, Results.Count > 0 ? Results.Count : 10);
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); } }
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; private set { _progress = value; OnPropertyChanged(); } }

    public IReadOnlyList<string> SelectedFiles
    {
        get => _selectedFiles;
        set
        {
            _selectedFiles = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionSummary));
        }
    }

    public string SelectionSummary => SelectedFiles.Count == 0 ? "No photos selected" : $"{SelectedFiles.Count} photo(s) selected";
    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task ApplyAsync(string overlayPath, CancellationToken token = default)
    {
        if (IsBusy) return;
        if (SelectedFiles.Count == 0) throw new InvalidOperationException("Select at least one image first.");

        IsBusy = true;
        Results.Clear();
        Progress = 0;
        var failures = 0;
        try
        {
            Status = "Analyzing photo orientations…";
            var plan = await ImageProcessingService.PlanBatchAsync(SelectedFiles, token);
            var total = plan.Count;

            for (var i = 0; i < total; i++)
            {
                token.ThrowIfCancellationRequested();
                var item = plan[i];
                var itemDescription = item switch
                {
                    ImageBatchItem.PortraitPair pair => $"Pair: {Path.GetFileName(pair.LeftFilePath)} + {Path.GetFileName(pair.RightFilePath)}",
                    ImageBatchItem.Landscape landscape => Path.GetFileName(landscape.FilePath),
                    ImageBatchItem.LonePortrait lone => Path.GetFileName(lone.FilePath),
                    _ => string.Empty
                };

                Status = $"Processing {i + 1} of {total} ({itemDescription})…";
                try
                {
                    Results.Add(await _processor.ProcessBatchItemAsync(item, overlayPath, Prefix, i, total, token));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures++;
                    Status = $"Skipped {itemDescription}: {ex.Message}";
                }

                Progress = (i + 1d) / total * 100;
            }

            RenameResults();
            Status = failures == 0
                ? $"Completed {Results.Count} image(s)."
                : $"Completed {Results.Count} image(s); skipped {failures}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveImageAsync(BrandedImage image, string path)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Status = $"Saving {image.FileName}…";
            await File.WriteAllBytesAsync(path, image.ImageBytes);
            Status = "Image export complete.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ExportZipAsync(string path)
    {
        if (IsBusy) return;
        if (Results.Count == 0) throw new InvalidOperationException("Apply branding before exporting.");

        IsBusy = true;
        var temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            Status = "Creating ZIP export…";
            await using (var file = File.Create(temporaryPath))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                var folder = FileNameGenerator.FolderName(Prefix);
                for (var i = 0; i < Results.Count; i++)
                {
                    var result = Results[i];
                    var fileName = FileNameGenerator.Generate(Prefix, result.SequenceIndex, result.BatchSize);
                    var entry = archive.CreateEntry($"{folder}/{fileName}");
                    await using var stream = entry.Open();
                    await stream.WriteAsync(result.ImageBytes);
                }
            }

            File.Move(temporaryPath, path, true);
            Status = "ZIP export complete.";
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { }
            }

            IsBusy = false;
        }
    }

    private void RenameResults()
    {
        for (var i = 0; i < Results.Count; i++)
        {
            var result = Results[i];
            result.FileName = FileNameGenerator.Generate(Prefix, result.SequenceIndex, result.BatchSize);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
