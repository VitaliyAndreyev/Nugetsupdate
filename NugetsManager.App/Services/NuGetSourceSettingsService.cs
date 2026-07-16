using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using NugetsManager.App.Models;

namespace NugetsManager.App.Services;

public sealed class NuGetSourceSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string DefaultMSBuildPath = @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe";

    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProjectManager",
        "nuget-sources.json");

    public ApplicationSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return CreateDefaultSettings();
        }

        var json = File.ReadAllText(_settingsPath);
        if (json.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            var legacySources = JsonSerializer.Deserialize<List<NuGetSourceSetting>>(json) ?? [];
            return new ApplicationSettings
            {
                MSBuildPath = DefaultMSBuildPath,
                NuGetSources = new ObservableCollection<NuGetSourceSetting>(NormalizeSources(legacySources))
            };
        }

        var settings = JsonSerializer.Deserialize<ApplicationSettings>(json) ?? CreateDefaultSettings();
        settings.MSBuildPath = string.IsNullOrWhiteSpace(settings.MSBuildPath)
            ? DefaultMSBuildPath
            : settings.MSBuildPath;
        return new ApplicationSettings
        {
            MSBuildPath = settings.MSBuildPath,
            NuGetSources = new ObservableCollection<NuGetSourceSetting>(NormalizeSources(settings.NuGetSources))
        };
    }

    public void Save(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath) ?? ".");
        var normalizedSettings = new ApplicationSettings
        {
            MSBuildPath = settings.MSBuildPath.Trim(),
            NuGetSources = new ObservableCollection<NuGetSourceSetting>(NormalizeSources(settings.NuGetSources))
        };

        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(normalizedSettings, JsonOptions));
    }

    private static ApplicationSettings CreateDefaultSettings()
        => new()
        {
            MSBuildPath = DefaultMSBuildPath,
            NuGetSources = [new NuGetSourceSetting { Source = "https://nuget02.3tec.de/api/v2" }]
        };

    private static List<NuGetSourceSetting> NormalizeSources(IEnumerable<NuGetSourceSetting> sources)
        => sources
            .Select(source => new NuGetSourceSetting { Source = source.Source.Trim() })
            .Where(source => !string.IsNullOrWhiteSpace(source.Source))
            .DistinctBy(source => source.Source, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
