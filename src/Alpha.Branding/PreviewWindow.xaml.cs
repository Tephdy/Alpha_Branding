using Alpha.Branding.Models;
using Alpha.Branding.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Alpha.Branding;

public partial class PreviewWindow : System.Windows.Window, INotifyPropertyChanged
{
    private readonly IReadOnlyList<BrandedImage> _results;
    private int _selectedIndex;

    public PreviewWindow(IReadOnlyList<BrandedImage> results, int selectedIndex)
    {
        InitializeComponent();
        WindowThemeHelper.EnableDarkTitleBar(this);
        _results = results;
        _selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, results.Count - 1));
        DataContext = this;
    }

    public BrandedImage? Current => _results.Count == 0 ? null : _results[_selectedIndex];
    public string PositionText => _results.Count == 0 ? "0 of 0" : $"{_selectedIndex + 1} of {_results.Count}";
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Previous_Click(object sender, System.Windows.RoutedEventArgs e) => Move(-1);
    private void Next_Click(object sender, System.Windows.RoutedEventArgs e) => Move(1);
    private void Move(int delta)
    {
        if (_results.Count == 0) return;
        _selectedIndex = (_selectedIndex + delta + _results.Count) % _results.Count;
        PropertyChanged?.Invoke(this, new(nameof(Current)));
        PropertyChanged?.Invoke(this, new(nameof(PositionText)));
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.Left) Move(-1);
        else if (e.Key == Key.Right) Move(1);
    }
}
