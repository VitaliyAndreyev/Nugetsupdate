using NugetsManager.App.Models;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Xml.Linq;

namespace NugetsManager.App.Services;

public sealed class NuGetUpdateService(PowerShellCommandRunner commandRunner)
{
  private static readonly HttpClient HttpClient = new();

  public async Task<IReadOnlyList<PackageUpdate>> CheckUpdatesAsync(ProjectNode project, IReadOnlyCollection<string> sources, CancellationToken cancellationToken = default)
  {
    var workingDirectory = Path.GetDirectoryName(project.Path) ?? Environment.CurrentDirectory;
    var result = await commandRunner.RunAsync($"dotnet list \"{project.Path}\" package --include-transitive --format json",
        workingDirectory,
        cancellationToken);

    var packages = result.Succeeded
        ? ParsePackageReferences(project.Name, result.Output)
        : [];

    if (packages.Count == 0)
    {
      var netFrameworkResult = await commandRunner.RunAsync($"dotnet list \"{project.Path}\" package --framework net4.8 --include-transitive --format json",
          workingDirectory,
          cancellationToken);
      var netFrameworkPackages = netFrameworkResult.Succeeded
          ? ParsePackageReferences(project.Name, netFrameworkResult.Output)
          : [];

      // A successful fallback with no package references is still a successful
      // project check. Keep the original result only when the fallback itself
      // failed, so an empty project is not reported as an analysis error.
      if (netFrameworkResult.Succeeded)
      {
        result = netFrameworkResult;
        packages = netFrameworkPackages;
      }
    }

    if (!result.Succeeded)
    {
      throw new InvalidOperationException(result.Error.Length > 0 ? result.Error : result.Output);
    }

    if (packages.Count == 0)
    {
      return [];
    }

    var updates = new List<PackageUpdate>();

    foreach (var package in packages)
    {
      var availableVersions = await ResolveAvailableVersionsAsync(package.PackageName, sources, cancellationToken);
      if (availableVersions.Count == 0)
      {
        continue;
      }

      var selectableVersions = availableVersions
          .Append(package.CurrentVersion)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderByDescending(version => version, PackageVersionComparer.Instance)
          .ToList();
      var latestAvailableVersion = availableVersions.FirstOrDefault();
      var latestVersion = latestAvailableVersion is not null &&
                          PackageVersionComparer.Instance.Compare(latestAvailableVersion, package.CurrentVersion) > 0
          ? latestAvailableVersion
          : package.CurrentVersion;

      var update = new PackageUpdate
      {
        ProjectName = package.ProjectName,
        PackageName = package.PackageName,
        CurrentVersion = package.CurrentVersion,
        LatestVersion = latestVersion,
        TargetVersion = latestVersion,
        IsSelected = PackageVersionComparer.Instance.Compare(latestVersion, package.CurrentVersion) > 0
      };

      foreach (var version in selectableVersions)
      {
        update.AvailableVersions.Add(version);
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
      var packageVersion = string.IsNullOrWhiteSpace(currentVersion) ? requestedVersion : currentVersion;

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

  private static async Task<IReadOnlyList<string>> ResolveAvailableVersionsAsync(string packageName, IReadOnlyCollection<string> sources, CancellationToken cancellationToken)
  {
    if (sources.Count == 0)
    {
      return [];
    }

    var versions = new List<string>();
    foreach (var source in sources.Where(source => !string.IsNullOrWhiteSpace(source)))
    {
      versions.AddRange(await ReadVersionsFromSourceAsync(source.Trim(), packageName, cancellationToken));
    }

    var releaseVersions = versions
        .Where(IsAllowedVersion)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(version => version, PackageVersionComparer.Instance)
        .ToList();

    if (releaseVersions.Count == 0)
    {
      return [];
    }

    return releaseVersions;
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
      var visitedUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var versions = new List<string>();

      while (!string.IsNullOrWhiteSpace(requestUri) && visitedUris.Add(requestUri))
      {
        using var stream = await HttpClient.GetStreamAsync(requestUri, cancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        versions.AddRange(document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value)
            .Where(version => !string.IsNullOrWhiteSpace(version)));

        var nextUri = document
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("link", StringComparison.OrdinalIgnoreCase))
            .Where(element => string.Equals((string?)element.Attribute("rel"), "next", StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("href"))
            .FirstOrDefault(uri => !string.IsNullOrWhiteSpace(uri));

        requestUri = ResolveNextPageUri(requestUri, nextUri);
      }

      return versions;
    }
    catch
    {
      return [];
    }
  }

  private static string? ResolveNextPageUri(string currentUri, string? nextUri)
  {
    if (string.IsNullOrWhiteSpace(nextUri))
    {
      return null;
    }

    return Uri.TryCreate(nextUri, UriKind.Absolute, out var absoluteUri)
        ? absoluteUri.ToString()
        : new Uri(new Uri(currentUri), nextUri).ToString();
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

  private static bool IsAllowedVersion(string version)
      => !version.Contains("alpha", StringComparison.OrdinalIgnoreCase) &&
         !version.Contains('-', StringComparison.OrdinalIgnoreCase);

}
