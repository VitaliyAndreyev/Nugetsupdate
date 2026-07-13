using ProjectManager.App.Models;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;

namespace ProjectManager.App.Services;

public sealed class NuGetUpdateService(PowerShellCommandRunner commandRunner)
{
  private static readonly HttpClient HttpClient = new();

  public async Task<IReadOnlyList<PackageUpdate>> CheckUpdatesAsync(ProjectNode project, IReadOnlyCollection<string> sources, CancellationToken cancellationToken = default)
  {
    var result = await commandRunner.RunAsync($"dotnet list \"{project.Path}\" package --framework net4.8 --include-transitive --format json",
        Path.GetDirectoryName(project.Path) ?? Environment.CurrentDirectory,
        cancellationToken);

    if (!result.Succeeded)
    {
      throw new InvalidOperationException(result.Error.Length > 0 ? result.Error : result.Output);
    }

    var packages = ParsePackageReferences(project.Name, result.Output);
    var updates = new List<PackageUpdate>();

    foreach (var package in packages)
    {
      var latestVersion = await ResolveLatestVersionAsync(package.PackageName, package.CurrentVersion, sources, cancellationToken);
      if (string.IsNullOrWhiteSpace(latestVersion))
      {
        continue;
      }

      var update = new PackageUpdate
      {
        ProjectName = package.ProjectName,
        PackageName = package.PackageName,
        CurrentVersion = package.CurrentVersion,
        LatestVersion = latestVersion,
        TargetVersion = latestVersion,
        IsSelected = VersionStringComparer.Instance.Compare(latestVersion, package.CurrentVersion) > 0
      };

      update.AvailableVersions.Add(latestVersion);
      if (!VersionsAreEqual(package.CurrentVersion, latestVersion))
      {
        update.AvailableVersions.Add(package.CurrentVersion);
      }

      updates.Add(update);
    }

    return updates
        .GroupBy(update => update.PackageName, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(update => update.PackageName)
        .ToList();
  }

  public async Task<ProcessResult> ApplyUpdateAsync(ProjectNode project, PackageUpdate packageUpdate, IReadOnlyCollection<string> sources, CancellationToken cancellationToken = default)
  {
    var sourceArguments = BuildSourceArguments(sources, "--source");
    var command = $"dotnet add \"{project.Path}\" package \"{packageUpdate.PackageName}\" --version \"{packageUpdate.TargetVersion}\" {sourceArguments}";
    return await commandRunner.RunAsync(command, Path.GetDirectoryName(project.Path) ?? Environment.CurrentDirectory, cancellationToken);
  }

  private static IReadOnlyList<PackageUpdate> ParsePackageReferences(string projectName, string json)
  {
    using var document = JsonDocument.Parse(json);
    var packages = new List<PackageUpdate>();

    if (!document.RootElement.TryGetProperty("projects", out var projects))
    {
      return packages;
    }

    foreach (var projectElement in projects.EnumerateArray())
    {
      if (!projectElement.TryGetProperty("frameworks", out var frameworks))
      {
        continue;
      }

      foreach (var framework in frameworks.EnumerateArray())
      {
        ReadPackageGroup(projectName, framework, "topLevelPackages", packages);
      }
    }

    return packages
        .GroupBy(package => package.PackageName, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .OrderBy(package => package.PackageName)
        .ToList();
  }

  private static void ReadPackageGroup(
      string projectName,
      JsonElement framework,
      string propertyName,
      List<PackageUpdate> packages)
  {
    if (!framework.TryGetProperty(propertyName, out var packageElements))
    {
      return;
    }

    foreach (var package in packageElements.EnumerateArray())
    {
      var currentVersion = GetString(package, "resolvedVersion");
      var requestedVersion = GetString(package, "requestedVersion");
      var packageName = GetString(package, "id");
      var packageVersion = string.IsNullOrWhiteSpace(requestedVersion) ? currentVersion : requestedVersion;

      if (string.IsNullOrWhiteSpace(packageName) ||
          string.IsNullOrWhiteSpace(packageVersion))
      {
        continue;
      }

      packages.Add(new PackageUpdate
      {
        ProjectName = projectName,
        PackageName = packageName,
        CurrentVersion = packageVersion,
        LatestVersion = packageVersion,
        TargetVersion = packageVersion,
        IsSelected = false
      });
    }
  }

  private static string GetString(JsonElement element, string propertyName)
      => element.TryGetProperty(propertyName, out var property) ? property.GetString() ?? string.Empty : string.Empty;

  private static string BuildSourceArguments(IReadOnlyCollection<string> sources, string argumentName)
  {
    return string.Join(" ", sources
        .Where(source => !string.IsNullOrWhiteSpace(source))
        .Select(source => $"{argumentName} \"{source.Trim()}\""));
  }

  private static async Task<string?> ResolveLatestVersionAsync(string packageName, string currentVersion, IReadOnlyCollection<string> sources, CancellationToken cancellationToken)
  {
    if (sources.Count == 0)
    {
      return null;
    }

    var versions = new List<string>();
    foreach (var source in sources.Where(source => !string.IsNullOrWhiteSpace(source)))
    {
      versions.AddRange(await ReadVersionsFromSourceAsync(source.Trim(), packageName, cancellationToken));
    }

    var releaseVersions = versions
        .Where(IsAllowedVersion)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(version => version, VersionStringComparer.Instance)
        .ToList();

    if (releaseVersions.Count == 0)
    {
      return null;
    }

    return releaseVersions.FirstOrDefault(version => VersionStringComparer.Instance.Compare(version, currentVersion) >= 0)
        ?? currentVersion;
  }

  private static async Task<IReadOnlyList<string>> ReadVersionsFromSourceAsync(string source, string packageName, CancellationToken cancellationToken)
  {
    if (source.EndsWith("index.json", StringComparison.OrdinalIgnoreCase))
    {
      return await ReadVersionsFromV3SourceAsync(source, packageName, cancellationToken);
    }

    var v2Versions = await ReadVersionsFromV2SourceAsync(source, packageName, cancellationToken);
    if (v2Versions.Count > 0)
    {
      return v2Versions;
    }

    return source.Contains("/api/v2", StringComparison.OrdinalIgnoreCase)
        ? v2Versions
        : await ReadVersionsFromV3SourceAsync($"{source.TrimEnd('/')}/index.json", packageName, cancellationToken);
  }

  private static async Task<IReadOnlyList<string>> ReadVersionsFromV2SourceAsync(string source, string packageName, CancellationToken cancellationToken)
  {
    try
    {
      var requestUri = $"{source.TrimEnd('/')}/FindPackagesById()?id='{Uri.EscapeDataString(packageName)}'";
      using var stream = await HttpClient.GetStreamAsync(requestUri, cancellationToken);
      var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
      return document
          .Descendants()
          .Where(element => element.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase))
          .Select(element => element.Value)
          .Where(version => !string.IsNullOrWhiteSpace(version))
          .ToList();
    }
    catch
    {
      return [];
    }
  }

  private static async Task<IReadOnlyList<string>> ReadVersionsFromV3SourceAsync(string source, string packageName, CancellationToken cancellationToken)
  {
    try
    {
      using var indexStream = await HttpClient.GetStreamAsync(source, cancellationToken);
      using var indexDocument = await JsonDocument.ParseAsync(indexStream, cancellationToken: cancellationToken);
      if (!indexDocument.RootElement.TryGetProperty("resources", out var resources))
      {
        return [];
      }

      var packageBaseAddress = resources
          .EnumerateArray()
          .Where(resource => GetString(resource, "@type").Contains("PackageBaseAddress", StringComparison.OrdinalIgnoreCase))
          .Select(resource => GetString(resource, "@id"))
          .FirstOrDefault();

      if (string.IsNullOrWhiteSpace(packageBaseAddress))
      {
        return [];
      }

      var packageIndexUri = $"{packageBaseAddress.TrimEnd('/')}/{packageName.ToLowerInvariant()}/index.json";
      using var packageStream = await HttpClient.GetStreamAsync(packageIndexUri, cancellationToken);
      using var packageDocument = await JsonDocument.ParseAsync(packageStream, cancellationToken: cancellationToken);
      return packageDocument.RootElement.TryGetProperty("versions", out var versions)
          ? versions.EnumerateArray().Select(version => version.GetString() ?? string.Empty).Where(version => !string.IsNullOrWhiteSpace(version)).ToList()
          : [];
    }
    catch
    {
      return [];
    }
  }

  private static bool VersionsAreEqual(string left, string right)
      => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

  private static bool IsAllowedVersion(string version)
      => !version.Contains("alpha", StringComparison.OrdinalIgnoreCase) &&
         !version.Contains('-', StringComparison.OrdinalIgnoreCase);

  private sealed class VersionStringComparer : IComparer<string>
  {
    public static readonly VersionStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
      if (ReferenceEquals(x, y))
      {
        return 0;
      }

      if (x is null)
      {
        return -1;
      }

      if (y is null)
      {
        return 1;
      }

      var leftParts = SplitVersion(x);
      var rightParts = SplitVersion(y);
      var maxLength = Math.Max(leftParts.Length, rightParts.Length);

      for (var index = 0; index < maxLength; index++)
      {
        var left = index < leftParts.Length ? leftParts[index] : 0;
        var right = index < rightParts.Length ? rightParts[index] : 0;
        var comparison = left.CompareTo(right);
        if (comparison != 0)
        {
          return comparison;
        }
      }

      return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    private static long[] SplitVersion(string version)
        => version
            .Split('-', 2)[0]
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => long.TryParse(part, out var number) ? number : 0)
            .ToArray();
  }
}
