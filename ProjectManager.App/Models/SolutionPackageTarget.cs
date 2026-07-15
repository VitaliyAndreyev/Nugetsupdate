using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectManager.App.Models;

public sealed class SolutionPackageTarget : INotifyPropertyChanged
{
  private string _targetVersion = string.Empty;
  private bool _isSelected = true;

  public event PropertyChangedEventHandler? PropertyChanged;

  public string PackageName { get; init; } = string.Empty;
  public int ProjectCount { get; init; }
  public string ProjectNames { get; init; } = string.Empty;
  public string CurrentVersions { get; init; } = string.Empty;
  public ObservableCollection<string> AvailableVersions { get; } = [];

  public string TargetVersion
  {
    get => _targetVersion;
    set
    {
      if (SetField(ref _targetVersion, value))
      {
        IsSelected = !string.IsNullOrWhiteSpace(value);
      }
    }
  }

  public bool IsSelected
  {
    get => _isSelected;
    set => SetField(ref _isSelected, value);
  }

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
    {
      return false;
    }

    field = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    return true;
  }
}
