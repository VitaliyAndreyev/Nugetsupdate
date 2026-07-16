using Microsoft.Win32;
using NugetsManager.App.Infrastructure;
using NugetsManager.App.Models;
using NugetsManager.App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Data;

namespace NugetsManager.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
  private const int MaxConcurrentProjects = 2;
  private readonly SolutionAnalyzer _solutionAnalyzer = new();
  private readonly PowerShellCommandRunner _commandRunner = new();
  private readonly NuGetUpdateService _nugetUpdateService;
  private readonly ReleaseWorkflowService _releaseWorkflowService;
  private readonly ProjectVersionService _projectVersionService = new();
  private readonly NuGetSourceSettingsService _sourceSettingsService = new();
  private ProjectNode? _selectedProject;
  private string _solutionPath = string.Empty;
  private string _newVersion = string.Empty;
  private string _tagPattern = "V_{version}";
  private string _commitMessage = "Update NuGet packages";
  private string _msBuildPath = string.Empty;
  private string _status = "Select a solution to begin.";
  private string _workflowLog = string.Empty;
  private string _packageGroupMatchMode = "Prefix";
  private string _packageGroupPattern = string.Empty;
  private string _packageGroupTargetVersion = string.Empty;
  private string _packageGroupMatchSummary = "Enter a package prefix or name.";
  private bool _isBusy;

  public MainViewModel()
  {
    SolutionPackageTargetsView = CollectionViewSource.GetDefaultView(SolutionPackageTargets);
    SolutionPackageTargetsView.Filter = FilterSolutionPackageTarget;
    _nugetUpdateService = new NuGetUpdateService(_commandRunner);
    _releaseWorkflowService = new ReleaseWorkflowService(_commandRunner);
    BrowseSolutionCommand = new RelayCommand(BrowseSolution);
    SaveNuGetSourcesCommand = new RelayCommand(SaveNuGetSources);
    AddNuGetSourceCommand = new RelayCommand(() => NuGetSources.Add(new NuGetSourceSetting()));
    RemoveEmptyNuGetSourcesCommand = new RelayCommand(RemoveEmptyNuGetSources);
    SetSolutionVersionCommand = new RelayCommand(SetSolutionVersion, () => Projects.Count > 0 && !string.IsNullOrWhiteSpace(NewVersion) && !IsBusy);
    SelectAllPackageUpdatesCommand = new RelayCommand(SelectAllPackageUpdates, () => HasPackageRows && !IsBusy);
    ClearAllPackageUpdatesCommand = new RelayCommand(ClearAllPackageUpdates, () => HasPackageRows && !IsBusy);
    ApplyPackageTargetsCommand = new RelayCommand(ApplyPackageTargets, () => SolutionPackageTargets.Count > 0 && !IsBusy);
    SelectLatestPackageTargetsCommand = new RelayCommand(SelectLatestPackageTargets, () => SolutionPackageTargets.Count > 0 && !IsBusy);
    ClearPackageTargetsCommand = new RelayCommand(ClearPackageTargets, () => SolutionPackageTargets.Count > 0 && !IsBusy);
    ApplyPackageGroupTargetCommand = new RelayCommand(ApplyPackageGroupTarget, () => PackageGroupAvailableVersions.Count > 0 && !string.IsNullOrWhiteSpace(PackageGroupTargetVersion) && !IsBusy);
    CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !string.IsNullOrWhiteSpace(SolutionPath) && !IsBusy);
    ApplyUpdatesCommand = new AsyncRelayCommand(ApplyUpdatesAsync, () => Projects.SelectMany(project => project.PackageUpdates).Any(update => update.IsSelected && update.CanApply) && !IsBusy);
    BuildCommand = new AsyncRelayCommand(BuildAsync, () => !string.IsNullOrWhiteSpace(SolutionPath) && !IsBusy);
    CommitCommand = new AsyncRelayCommand(CommitAndPushAsync, () => !string.IsNullOrWhiteSpace(SolutionPath) && !string.IsNullOrWhiteSpace(CommitMessage) && !IsBusy);
    CreateTagCommand = new AsyncRelayCommand(CreateAndPushTagAsync, () => !string.IsNullOrWhiteSpace(SolutionPath) && !string.IsNullOrWhiteSpace(ResolvedTagName) && !IsBusy);

    var settings = _sourceSettingsService.Load();
    MSBuildPath = settings.MSBuildPath;
    foreach (var source in settings.NuGetSources)
    {
      NuGetSources.Add(source);
    }
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<ProjectNode> Projects { get; } = [];
  public ObservableCollection<ProjectNode> ProjectTree { get; } = [];
  public ObservableCollection<PackageUpdate> PackageUpdates { get; } = [];
  public ObservableCollection<SolutionPackageTarget> SolutionPackageTargets { get; } = [];
  public ICollectionView SolutionPackageTargetsView { get; }
  public ObservableCollection<string> PackageGroupMatchModes { get; } = ["Prefix", "Contains", "Exact name"];
  public ObservableCollection<string> PackageGroupAvailableVersions { get; } = [];
  public ObservableCollection<NuGetSourceSetting> NuGetSources { get; } = [];

  public RelayCommand BrowseSolutionCommand { get; }
  public RelayCommand SaveNuGetSourcesCommand { get; }
  public RelayCommand AddNuGetSourceCommand { get; }
  public RelayCommand RemoveEmptyNuGetSourcesCommand { get; }
  public RelayCommand SetSolutionVersionCommand { get; }
  public RelayCommand SelectAllPackageUpdatesCommand { get; }
  public RelayCommand ClearAllPackageUpdatesCommand { get; }
  public RelayCommand ApplyPackageTargetsCommand { get; }
  public RelayCommand SelectLatestPackageTargetsCommand { get; }
  public RelayCommand ClearPackageTargetsCommand { get; }
  public RelayCommand ApplyPackageGroupTargetCommand { get; }
  public AsyncRelayCommand CheckUpdatesCommand { get; }
  public AsyncRelayCommand ApplyUpdatesCommand { get; }
  public AsyncRelayCommand BuildCommand { get; }
  public AsyncRelayCommand CommitCommand { get; }
  public AsyncRelayCommand CreateTagCommand { get; }

  public string SolutionPath
  {
    get => _solutionPath;
    private set => SetField(ref _solutionPath, value);
  }

  public string PackageGroupMatchMode
  {
    get => _packageGroupMatchMode;
    set
    {
      if (SetField(ref _packageGroupMatchMode, value))
      {
        RefreshPackageGroupSelection();
      }
    }
  }

  public string PackageGroupPattern
  {
    get => _packageGroupPattern;
    set
    {
      if (SetField(ref _packageGroupPattern, value))
      {
        RefreshPackageGroupSelection();
      }
    }
  }

  public string PackageGroupTargetVersion
  {
    get => _packageGroupTargetVersion;
    set
    {
      if (SetField(ref _packageGroupTargetVersion, value))
      {
        ApplyPackageGroupTargetCommand.RaiseCanExecuteChanged();
      }
    }
  }

  public string PackageGroupMatchSummary
  {
    get => _packageGroupMatchSummary;
    private set => SetField(ref _packageGroupMatchSummary, value);
  }

  public string NewVersion
  {
    get => _newVersion;
    set
    {
      if (SetField(ref _newVersion, value))
      {
        SetSolutionVersionCommand.RaiseCanExecuteChanged();
        CreateTagCommand.RaiseCanExecuteChanged();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ResolvedTagName)));
      }
    }
  }

  public string TagPattern
  {
    get => _tagPattern;
    set
    {
      if (SetField(ref _tagPattern, value))
      {
        CreateTagCommand.RaiseCanExecuteChanged();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ResolvedTagName)));
      }
    }
  }

  public string ResolvedTagName => ResolveTagName();

  public string CommitMessage
  {
    get => _commitMessage;
    set
    {
      if (SetField(ref _commitMessage, value))
      {
        CommitCommand.RaiseCanExecuteChanged();
      }
    }
  }

  public string MSBuildPath
  {
    get => _msBuildPath;
    set => SetField(ref _msBuildPath, value);
  }

  public string SolutionVersion => ResolveSolutionVersion();

  public string SelectedProjectVersion => SelectedProject?.ProjectVersion ?? string.Empty;

  public ProjectNode? SelectedProject
  {
    get => _selectedProject;
    set
    {
      if (SetField(ref _selectedProject, value))
      {
        RefreshProjectTree();
        RefreshPackageGrid();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedProjectVersion)));
        RaiseCommandStates();
      }
    }
  }

  public string Status
  {
    get => _status;
    private set => SetField(ref _status, value);
  }

  public string WorkflowLog
  {
    get => _workflowLog;
    private set => SetField(ref _workflowLog, value);
  }

  public bool IsBusy
  {
    get => _isBusy;
    private set
    {
      if (SetField(ref _isBusy, value))
      {
        RaiseCommandStates();
      }
    }
  }

  private bool HasPackageRows => Projects.SelectMany(project => project.PackageUpdates).Any();

  private void BrowseSolution()
  {
    var dialog = new OpenFileDialog
    {
      Filter = "Visual Studio solution (*.sln)|*.sln",
      Title = "Select solution"
    };

    if (dialog.ShowDialog() != true)
    {
      return;
    }

    LoadSolution(dialog.FileName);
  }

  private void LoadSolution(string solutionPath)
  {
    SolutionPath = solutionPath;
    Projects.Clear();
    ProjectTree.Clear();
    PackageUpdates.Clear();
    SolutionPackageTargets.Clear();
    PackageGroupAvailableVersions.Clear();
    PackageGroupPattern = string.Empty;
    PackageGroupTargetVersion = string.Empty;
    PackageGroupMatchSummary = "Enter a package prefix or name.";

    foreach (var project in _solutionAnalyzer.LoadProjects(solutionPath))
    {
      project.ProjectVersion = _projectVersionService.ReadVersion(project);
      Projects.Add(project);
      ProjectTree.Add(project);
    }

    SelectedProject = Projects.FirstOrDefault();
    NewVersion = ResolveInitialVersion();
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SolutionVersion)));
    RaiseCommandStates();
    AppendLog("Solution loaded", $"Path: {solutionPath}{Environment.NewLine}Projects: {Projects.Count}{Environment.NewLine}Current version: {SolutionVersion}");
  }

  private void SetSolutionVersion()
  {
    var version = NewVersion.Trim();
    foreach (var project in Projects)
    {
      _projectVersionService.WriteVersion(project, version);
    }

    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedProjectVersion)));
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SolutionVersion)));
    AppendLog("Version set", $"Version {version} was written to {Projects.Count} project(s).{Environment.NewLine}{string.Join(Environment.NewLine, Projects.Select(project => $"- {project.Name}: {version}"))}");
  }

  private async Task CheckUpdatesAsync()
  {
    await RunStageAsync("Checking NuGet updates...", async () =>
    {
      if (Projects.Count == 0)
      {
        AppendLog("Check updates skipped", "No projects were loaded from the selected solution.");
        return;
      }

      var sources = ActiveNuGetSources();
      var log = new StringBuilder();
      log.AppendLine("NuGet sources:");
      foreach (var source in sources.DefaultIfEmpty("<default NuGet configuration>"))
      {
        log.AppendLine($"- {source}");
      }
      log.AppendLine();

      foreach (var project in Projects)
      {
        project.WorkflowStatus = "Pending";
      }
      await Task.Yield();

      var checkResults = await RunWithConcurrencyAsync(Projects.ToList(), MaxConcurrentProjects, async project =>
      {
        var projectLog = new StringBuilder();
        project.WorkflowStatus = "Working";
        await Task.Yield();

        foreach (var existingUpdate in project.PackageUpdates)
        {
          existingUpdate.PropertyChanged -= PackageUpdate_PropertyChanged;
        }

        project.PackageUpdates.Clear();
        try
        {
          projectLog.AppendLine($"Checking {project.Name}...");
          var updates = await _nugetUpdateService.CheckUpdatesAsync(project, sources);
          foreach (var update in updates)
          {
            update.PropertyChanged += PackageUpdate_PropertyChanged;
            project.PackageUpdates.Add(update);
          }

          project.RefreshDisplayName();
          projectLog.AppendLine($"{project.Name}: {project.PackageUpdates.Count} package(s) found");
          foreach (var update in project.PackageUpdates)
          {
            projectLog.AppendLine($"  - {update.PackageName}: {update.CurrentVersion} -> {update.LatestVersion}");
          }

          project.WorkflowStatus = "Completed";
        }
        catch (Exception exception)
        {
          project.WorkflowStatus = "Failed";
          project.RefreshDisplayName();
          projectLog.AppendLine($"{project.Name}: check failed");
          projectLog.AppendLine($"  {exception.Message}");
        }

        return projectLog.ToString();
      });

      foreach (var projectLog in checkResults)
      {
        log.Append(projectLog);
      }

      RefreshPackageGrid();
      RefreshSolutionPackageTargets();
      var updateCount = Projects.Sum(project => project.PackageUpdates.Count);
      log.AppendLine();
      log.AppendLine($"Total: {updateCount} package(s) found across {Projects.Count} project(s).");
      AppendLog("Check updates completed", log.ToString().TrimEnd());
      RaiseCommandStates();
    });
  }

  private async Task ApplyUpdatesAsync()
  {
    await RunStageAsync("Applying selected package versions...", async () =>
    {
      var sources = ActiveNuGetSources();
      var log = new StringBuilder();
      var projectsToUpdate = Projects
          .Where(project => project.PackageUpdates.Any(update => update.IsSelected && update.CanApply))
          .ToList();

      foreach (var project in Projects)
      {
        project.WorkflowStatus = projectsToUpdate.Contains(project) ? "Pending" : string.Empty;
      }
      await Task.Yield();

      var applyResults = await RunWithConcurrencyAsync(projectsToUpdate, MaxConcurrentProjects, async project =>
      {
        var projectLog = new StringBuilder();
        var appliedCount = 0;
        project.WorkflowStatus = "Working";
        await Task.Yield();

        try
        {
          foreach (var update in project.PackageUpdates.Where(update => update.IsSelected && update.CanApply))
          {
            projectLog.AppendLine($"{project.Name}: {update.PackageName} -> {update.TargetVersion}");
            var result = await _nugetUpdateService.ApplyUpdateAsync(project, update, sources);
            if (!result.Succeeded)
            {
              projectLog.AppendLine($"{project.Name}: {update.PackageName} -> {update.TargetVersion} failed");
              projectLog.AppendLine(FormatProcessResult(result));
              throw new InvalidOperationException(result.Error.Length > 0 ? result.Error : result.Output);
            }

            appliedCount++;
          }

          project.WorkflowStatus = "Completed";
          return new ProjectApplyResult(project.Name, appliedCount, projectLog.ToString(), null);
        }
        catch (Exception exception)
        {
          project.WorkflowStatus = "Failed";
          projectLog.AppendLine($"{project.Name}: apply failed");
          projectLog.AppendLine($"  {exception.Message}");
          return new ProjectApplyResult(project.Name, appliedCount, projectLog.ToString(), exception.Message);
        }
      });

      foreach (var result in applyResults)
      {
        log.Append(result.Log);
      }

      var appliedCount = applyResults.Sum(result => result.AppliedCount);
      if (appliedCount == 0)
      {
        log.AppendLine("No selected package updates to apply.");
      }

      log.AppendLine();
      log.AppendLine($"Applied package updates: {appliedCount}");
      var failedProjects = applyResults.Where(result => result.Error is not null).ToList();
      if (failedProjects.Count > 0)
      {
        log.AppendLine($"Failed projects: {failedProjects.Count}");
        log.AppendLine(string.Join(Environment.NewLine, failedProjects.Select(result => $"- {result.ProjectName}: {result.Error}")));
        AppendLog("Apply versions completed with errors", log.ToString().TrimEnd());
        throw new InvalidOperationException($"Package updates failed for {failedProjects.Count} project(s).");
      }

      AppendLog("Apply versions completed", log.ToString().TrimEnd());
    });
  }

  private async Task BuildAsync()
  {
    await RunStageAsync("Building solution...", async () =>
    {
      var result = await _releaseWorkflowService.BuildAsync(SolutionPath, MSBuildPath);
      if (!result.Succeeded)
      {
        AppendLog("Build failed", FormatProcessResult(result));
        throw new InvalidOperationException(result.Error.Length > 0 ? result.Error : result.Output);
      }

      AppendLog("Build succeeded", FormatProcessResult(result));
    });
  }

  private async Task CommitAndPushAsync()
  {
    await RunStageAsync("Committing and pushing changes...", async () =>
    {
      var repositoryPath = Path.GetDirectoryName(SolutionPath) ?? Environment.CurrentDirectory;
      var commit = await _releaseWorkflowService.CommitAsync(repositoryPath, CommitMessage.Trim());
      if (!commit.Succeeded)
      {
        AppendLog("Commit failed", $"Message: {CommitMessage.Trim()}{Environment.NewLine}{FormatProcessResult(commit)}");
        throw new InvalidOperationException(commit.Error.Length > 0 ? commit.Error : commit.Output);
      }

      var push = await _releaseWorkflowService.PushAsync(repositoryPath);
      if (!push.Succeeded)
      {
        AppendLog("Push failed", FormatProcessResult(push));
        throw new InvalidOperationException(push.Error.Length > 0 ? push.Error : push.Output);
      }

      AppendLog("Commit and push completed", $"Message: {CommitMessage.Trim()}{Environment.NewLine}{FormatProcessResult(commit)}{Environment.NewLine}{FormatProcessResult(push)}");
    });
  }

  private async Task CreateAndPushTagAsync()
  {
    await RunStageAsync("Creating and pushing tag...", async () =>
    {
      var repositoryPath = Path.GetDirectoryName(SolutionPath) ?? Environment.CurrentDirectory;
      var tagName = ResolveTagName();
      var tag = await _releaseWorkflowService.CreateTagAsync(repositoryPath, tagName);
      if (!tag.Succeeded)
      {
        AppendLog("Create tag failed", $"Tag: {tagName}{Environment.NewLine}{FormatProcessResult(tag)}");
        throw new InvalidOperationException(tag.Error.Length > 0 ? tag.Error : tag.Output);
      }

      var pushTag = await _releaseWorkflowService.PushTagAsync(repositoryPath, tagName);
      if (!pushTag.Succeeded)
      {
        AppendLog("Push tag failed", $"Tag: {tagName}{Environment.NewLine}{FormatProcessResult(pushTag)}");
        throw new InvalidOperationException(pushTag.Error.Length > 0 ? pushTag.Error : pushTag.Output);
      }

      AppendLog("Tag created and pushed", $"Tag: {tagName}{Environment.NewLine}{FormatProcessResult(tag)}{Environment.NewLine}{FormatProcessResult(pushTag)}");
    });
  }

  private static async Task<TResult[]> RunWithConcurrencyAsync<T, TResult>(
      IReadOnlyList<T> items,
      int maxConcurrency,
      Func<T, Task<TResult>> action)
  {
    using var semaphore = new SemaphoreSlim(maxConcurrency);
    var tasks = items.Select(async item =>
    {
      await semaphore.WaitAsync();
      try
      {
        return await action(item);
      }
      finally
      {
        semaphore.Release();
      }
    });

    return await Task.WhenAll(tasks);
  }

  private async Task RunStageAsync(string stage, Func<Task> action)
  {
    try
    {
      IsBusy = true;
      AppendLog("Stage started", stage);
      await action();
    }
    catch (Exception exception)
    {
      Status = exception.Message;
      AppendLog("Stage failed", exception.Message);
    }
    finally
    {
      IsBusy = false;
    }
  }

  private void SelectAllPackageUpdates()
  {
    var selectedCount = 0;
    foreach (var update in Projects.SelectMany(project => project.PackageUpdates).Where(update => update.CanApply))
    {
      update.IsSelected = true;
      selectedCount++;
    }

    RefreshPackageGrid();
    AppendLog("Package selection changed", $"Use was enabled for {selectedCount} package row(s) across all projects.");
    RaiseCommandStates();
  }

  private void ClearAllPackageUpdates()
  {
    var clearedCount = 0;
    foreach (var update in Projects.SelectMany(project => project.PackageUpdates))
    {
      if (update.IsSelected)
      {
        clearedCount++;
      }

      update.IsSelected = false;
    }

    RefreshPackageGrid();
    AppendLog("Package selection changed", $"Use was disabled for {clearedCount} selected package row(s) across all projects.");
    RaiseCommandStates();
  }

  private void ApplyPackageTargets()
  {
    var updatedCount = 0;
    var alreadyCurrentCount = 0;
    var unavailableCount = 0;
    var log = new StringBuilder();

    foreach (var packageTarget in SolutionPackageTargets)
    {
      var packageUpdates = Projects
          .SelectMany(project => project.PackageUpdates)
          .Where(update => string.Equals(update.PackageName, packageTarget.PackageName, StringComparison.OrdinalIgnoreCase))
          .ToList();

      if (!packageTarget.IsSelected || string.IsNullOrWhiteSpace(packageTarget.TargetVersion))
      {
        foreach (var update in packageUpdates)
        {
          update.IsSelected = false;
        }

        continue;
      }

      log.AppendLine($"- {packageTarget.PackageName}: {packageTarget.TargetVersion} ({packageUpdates.Count} project(s))");
      foreach (var update in packageUpdates)
      {
        var availableVersion = update.AvailableVersions
            .FirstOrDefault(version => string.Equals(version, packageTarget.TargetVersion, StringComparison.OrdinalIgnoreCase));
        if (availableVersion is null)
        {
          update.IsSelected = false;
          unavailableCount++;
          continue;
        }

        update.TargetVersion = availableVersion;
        var requiresUpdate = !string.Equals(update.CurrentVersion, availableVersion, StringComparison.OrdinalIgnoreCase);
        update.IsSelected = requiresUpdate;
        if (requiresUpdate)
        {
          updatedCount++;
        }
        else
        {
          alreadyCurrentCount++;
        }
      }
    }

    RefreshPackageGrid();
    AppendLog(
        "Solution package targets applied",
        $"Package targets:{Environment.NewLine}{log}" +
        $"Selected updates: {updatedCount}{Environment.NewLine}" +
        $"Already at target: {alreadyCurrentCount}{Environment.NewLine}" +
        $"Unavailable target occurrences: {unavailableCount}");
    RaiseCommandStates();
  }

  private void SelectLatestPackageTargets()
  {
    foreach (var packageTarget in SolutionPackageTargets.Where(target => target.AvailableVersions.Count > 0))
    {
      packageTarget.TargetVersion = packageTarget.AvailableVersions[0];
      packageTarget.IsSelected = true;
    }
  }

  private void ClearPackageTargets()
  {
    foreach (var packageTarget in SolutionPackageTargets)
    {
      packageTarget.IsSelected = false;
    }
  }

  private void ApplyPackageGroupTarget()
  {
    var matchingTargets = MatchingPackageGroupTargets();
    var appliedCount = 0;
    foreach (var packageTarget in matchingTargets)
    {
      var matchingVersion = packageTarget.AvailableVersions
          .FirstOrDefault(version => string.Equals(version, PackageGroupTargetVersion, StringComparison.OrdinalIgnoreCase));
      if (matchingVersion is null)
      {
        continue;
      }

      packageTarget.TargetVersion = matchingVersion;
      packageTarget.IsSelected = true;
      appliedCount++;
    }

    AppendLog(
        "Package group target set",
        $"Match: {PackageGroupMatchMode} '{PackageGroupPattern.Trim()}'{Environment.NewLine}" +
        $"Target version: {PackageGroupTargetVersion}{Environment.NewLine}" +
        $"Packages updated in target table: {appliedCount}");
  }

  private void RefreshPackageGroupSelection()
  {
    SolutionPackageTargetsView.Refresh();
    var previousTarget = PackageGroupTargetVersion;
    PackageGroupAvailableVersions.Clear();

    var matchingTargets = MatchingPackageGroupTargets();
    if (matchingTargets.Count == 0)
    {
      PackageGroupMatchSummary = string.IsNullOrWhiteSpace(PackageGroupPattern)
          ? "Enter a package prefix or name."
          : "No matching packages.";
      PackageGroupTargetVersion = string.Empty;
      ApplyPackageGroupTargetCommand.RaiseCanExecuteChanged();
      return;
    }

    var commonVersions = matchingTargets[0].AvailableVersions
        .Where(version => matchingTargets.All(target => target.AvailableVersions.Any(candidate => string.Equals(candidate, version, StringComparison.OrdinalIgnoreCase))))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(version => version, PackageVersionComparer.Instance);
    foreach (var version in commonVersions)
    {
      PackageGroupAvailableVersions.Add(version);
    }

    PackageGroupTargetVersion = PackageGroupAvailableVersions
        .FirstOrDefault(version => string.Equals(version, previousTarget, StringComparison.OrdinalIgnoreCase))
        ?? PackageGroupAvailableVersions.FirstOrDefault()
        ?? string.Empty;
    PackageGroupMatchSummary = $"{matchingTargets.Count} matching package(s), {PackageGroupAvailableVersions.Count} common version(s).";
    ApplyPackageGroupTargetCommand.RaiseCanExecuteChanged();
  }

  private List<SolutionPackageTarget> MatchingPackageGroupTargets()
  {
    var pattern = PackageGroupPattern.Trim();
    if (string.IsNullOrWhiteSpace(pattern))
    {
      return [];
    }

    return SolutionPackageTargets
        .Where(MatchesPackageGroup)
        .ToList();
  }

  private bool FilterSolutionPackageTarget(object item)
      => item is SolutionPackageTarget target &&
         (string.IsNullOrWhiteSpace(PackageGroupPattern) || MatchesPackageGroup(target));

  private bool MatchesPackageGroup(SolutionPackageTarget target)
  {
    var pattern = PackageGroupPattern.Trim();
    return PackageGroupMatchMode switch
    {
      "Exact name" => string.Equals(target.PackageName, pattern, StringComparison.OrdinalIgnoreCase),
      "Contains" => target.PackageName.Contains(pattern, StringComparison.OrdinalIgnoreCase),
      _ => target.PackageName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)
    };
  }

  private void RefreshProjectTree()
  {
    RefreshPackageGrid();
  }

  private void RefreshPackageGrid()
  {
    PackageUpdates.Clear();
    if (SelectedProject is null)
    {
      return;
    }

    foreach (var update in SelectedProject.PackageUpdates)
    {
      PackageUpdates.Add(update);
    }
  }

  private void RefreshSolutionPackageTargets()
  {
    SolutionPackageTargets.Clear();

    var packageGroups = Projects
        .SelectMany(project => project.PackageUpdates)
        .GroupBy(update => update.PackageName, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

    foreach (var packageGroup in packageGroups)
    {
      var updates = packageGroup.ToList();
      var currentVersions = updates
          .Select(update => update.CurrentVersion)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderByDescending(version => version, PackageVersionComparer.Instance)
          .ToList();
      var projectNames = updates
          .Select(update => update.ProjectName)
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
          .ToList();
      var commonVersions = updates[0].AvailableVersions
          .Where(version => updates.All(update => update.AvailableVersions.Any(candidate => string.Equals(candidate, version, StringComparison.OrdinalIgnoreCase))))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .OrderByDescending(version => version, PackageVersionComparer.Instance)
          .ToList();

      var shouldSelect = commonVersions.Count > 0 && updates.Any(update => update.IsSelected);
      var target = new SolutionPackageTarget
      {
        PackageName = packageGroup.Key,
        ProjectCount = projectNames.Count,
        ProjectNames = string.Join(Environment.NewLine, projectNames),
        CurrentVersions = currentVersions.Count == 1 ? currentVersions[0] : $"Mixed: {string.Join(", ", currentVersions)}"
      };

      foreach (var version in commonVersions)
      {
        target.AvailableVersions.Add(version);
      }

      target.TargetVersion = commonVersions.FirstOrDefault() ?? string.Empty;
      target.IsSelected = shouldSelect;
      SolutionPackageTargets.Add(target);
    }

    RefreshPackageGroupSelection();
  }

  private void RaiseCommandStates()
  {
    SetSolutionVersionCommand.RaiseCanExecuteChanged();
    SelectAllPackageUpdatesCommand.RaiseCanExecuteChanged();
    ClearAllPackageUpdatesCommand.RaiseCanExecuteChanged();
    ApplyPackageTargetsCommand.RaiseCanExecuteChanged();
    SelectLatestPackageTargetsCommand.RaiseCanExecuteChanged();
    ClearPackageTargetsCommand.RaiseCanExecuteChanged();
    ApplyPackageGroupTargetCommand.RaiseCanExecuteChanged();
    CheckUpdatesCommand.RaiseCanExecuteChanged();
    ApplyUpdatesCommand.RaiseCanExecuteChanged();
    BuildCommand.RaiseCanExecuteChanged();
    CommitCommand.RaiseCanExecuteChanged();
    CreateTagCommand.RaiseCanExecuteChanged();
  }

  private void PackageUpdate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName is nameof(PackageUpdate.IsSelected) or nameof(PackageUpdate.TargetVersion) or nameof(PackageUpdate.CanApply))
    {
      ApplyUpdatesCommand.RaiseCanExecuteChanged();
    }
  }

  private void SaveNuGetSources()
  {
    RemoveEmptyNuGetSources();
    _sourceSettingsService.Save(new ApplicationSettings
    {
      MSBuildPath = MSBuildPath,
      NuGetSources = NuGetSources
    });
    AppendLog(
        "Settings saved",
        $"MSBuild: {MSBuildPath}{Environment.NewLine}NuGet sources:{Environment.NewLine}{string.Join(Environment.NewLine, NuGetSources.Select(source => $"- {source.Source}"))}");
  }

  private void RemoveEmptyNuGetSources()
  {
    foreach (var emptySource in NuGetSources.Where(source => string.IsNullOrWhiteSpace(source.Source)).ToList())
    {
      NuGetSources.Remove(emptySource);
    }
  }

  private IReadOnlyCollection<string> ActiveNuGetSources()
      => NuGetSources
          .Select(source => source.Source.Trim())
          .Where(source => !string.IsNullOrWhiteSpace(source))
          .Distinct(StringComparer.OrdinalIgnoreCase)
          .ToList();

  private string ResolveInitialVersion()
  {
    var versions = Projects
        .Select(project => project.ProjectVersion)
        .Where(version => !string.IsNullOrWhiteSpace(version))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    return versions.Count == 1 ? versions[0] : string.Empty;
  }

  private string ResolveSolutionVersion()
  {
    var versions = Projects
        .Select(project => project.ProjectVersion)
        .Where(version => !string.IsNullOrWhiteSpace(version))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    return versions.Count switch
    {
      0 => string.Empty,
      1 => versions[0],
      _ => "Mixed"
    };
  }

  private string ResolveTagName()
  {
    var version = NewVersion.Trim();
    if (string.IsNullOrWhiteSpace(version))
    {
      return string.Empty;
    }

    var pattern = string.IsNullOrWhiteSpace(TagPattern) ? "{version}" : TagPattern.Trim();
    return pattern.Replace("{version}", version, StringComparison.OrdinalIgnoreCase);
  }

  private void AppendLog(string title, string details)
  {
    Status = title;
    var entry = new StringBuilder();
    entry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}");
    if (!string.IsNullOrWhiteSpace(details))
    {
      entry.AppendLine(details.TrimEnd());
    }
    entry.AppendLine();

    WorkflowLog += entry.ToString();
  }

  private static string FormatProcessResult(ProcessResult result)
  {
    var builder = new StringBuilder();
    builder.AppendLine($"Exit code: {result.ExitCode}");
    AppendOutput(builder, "Output", result.Output);
    AppendOutput(builder, "Error", result.Error);
    return builder.ToString().TrimEnd();
  }

  private static void AppendOutput(StringBuilder builder, string title, string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return;
    }

    builder.AppendLine($"{title}:");
    builder.AppendLine(value.TrimEnd());
  }

  private sealed record ProjectApplyResult(
      string ProjectName,
      int AppliedCount,
      string Log,
      string? Error);

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
