using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ProjectManager.App.Models;

public sealed class ProjectNode : INotifyPropertyChanged
{
    private string _projectVersion = string.Empty;
    private string _workflowStatus = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public ObservableCollection<ProjectNode> Children { get; } = [];
    public ObservableCollection<PackageUpdate> PackageUpdates { get; } = [];

    public string ProjectVersion
    {
        get => _projectVersion;
        set
        {
            if (_projectVersion == value)
            {
                return;
            }

            _projectVersion = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProjectVersion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }

    public string WorkflowStatus
    {
        get => _workflowStatus;
        set
        {
            if (_workflowStatus == value)
            {
                return;
            }

            _workflowStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkflowStatus)));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(ProjectVersion)
        ? $"{Name} ({PackageUpdates.Count})"
        : $"{Name} v{ProjectVersion} ({PackageUpdates.Count})";

    public void RefreshDisplayName() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));

    public override string ToString() => Name;
}
