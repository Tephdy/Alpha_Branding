using System.ComponentModel;

namespace Alpha.Branding.Models;

public sealed class BrandedImage : INotifyPropertyChanged
{
    private string _fileName = string.Empty;

    public required string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName != value)
            {
                _fileName = value;
                PropertyChanged?.Invoke(this, new(nameof(FileName)));
            }
        }
    }

    public required byte[] ImageBytes { get; init; }
    public required System.Windows.Media.Imaging.BitmapImage Preview { get; init; }
    public int SequenceIndex { get; init; }
    public int BatchSize { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
