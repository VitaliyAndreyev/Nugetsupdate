using System.Collections.ObjectModel;

namespace NugetsManager.App.Models;

public sealed class ApplicationSettings
{
    public ObservableCollection<NuGetSourceSetting> NuGetSources { get; init; } = [];
    public string MSBuildPath { get; set; } = string.Empty;
}
