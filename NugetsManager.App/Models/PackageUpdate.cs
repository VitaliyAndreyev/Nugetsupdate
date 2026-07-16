using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NugetsManager.App.Models;

public sealed class PackageUpdate : INotifyPropertyChanged
{
  private static readonly string[] NotFoundMarkers =
  [
      "Not found at the sources",
        "Nicht in den Quellen gefunden"
  ];

  private string _targetVersion = string.Empty;
  private bool _isSelected = true;

  public event PropertyChangedEventHandler? PropertyChanged;

  public string ProjectName { get; init; } = string.Empty;
  public string PackageName { get; init; } = string.Empty;
  public string CurrentVersion { get; init; } = string.Empty;
  public string LatestVersion { get; init; } = string.Empty;
  public ObservableCollection<string> AvailableVersions { get; } = [];

  public string TargetVersion
  {
    get => _targetVersion;
    set
    {
      if (SetField(ref _targetVersion, value))
      {
        OnPropertyChanged(nameof(CanApply));
        IsSelected = CanApply && !string.Equals(CurrentVersion, value, StringComparison.OrdinalIgnoreCase);
      }
    }
  }

  public bool IsSelected
  {
    get => _isSelected;
    set => SetField(ref _isSelected, value);
  }

  public bool CanApply =>
      !IsNotFound(CurrentVersion) &&
      !IsNotFound(LatestVersion) &&
      !IsNotFound(TargetVersion) &&
      !string.IsNullOrWhiteSpace(TargetVersion);

  public static bool IsNotFound(string value)
      => NotFoundMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
    {
      return false;
    }

    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }

  private void OnPropertyChanged(string? propertyName)
      => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
