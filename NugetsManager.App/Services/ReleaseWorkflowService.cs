using System.IO;
using NugetsManager.App.Models;

namespace NugetsManager.App.Services;

public sealed class ReleaseWorkflowService(PowerShellCommandRunner commandRunner)
{
    public Task<ProcessResult> BuildAsync(string solutionPath, string msBuildPath, CancellationToken cancellationToken = default)
        => commandRunner.RunAsync(
            BuildMsBuildCommand(solutionPath, msBuildPath),
            Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory,
            cancellationToken);

    public Task<ProcessResult> CommitAsync(string repositoryPath, string message, CancellationToken cancellationToken = default)
        => commandRunner.RunAsync(
            $"git add -A; git commit -m \"{message}\"",
            repositoryPath,
            cancellationToken);

    public Task<ProcessResult> PushAsync(string repositoryPath, CancellationToken cancellationToken = default)
        => commandRunner.RunAsync("git push", repositoryPath, cancellationToken);

    public Task<ProcessResult> CreateTagAsync(string repositoryPath, string tagName, CancellationToken cancellationToken = default)
        => commandRunner.RunAsync($"git tag \"{tagName}\"", repositoryPath, cancellationToken);

    public Task<ProcessResult> PushTagAsync(string repositoryPath, string tagName, CancellationToken cancellationToken = default)
        => commandRunner.RunAsync($"git push origin \"{tagName}\"", repositoryPath, cancellationToken);

    private static string BuildMsBuildCommand(string solutionPath, string msBuildPath)
    {
        var quotedSolutionPath = QuotePowerShell(solutionPath);
        var quotedMSBuildPath = QuotePowerShell(msBuildPath);
        return """
               $msbuild = MSBUILD_PATH
               if (-not (Test-Path $msbuild)) { throw "MSBuild.exe was not found at $msbuild" }
               & $msbuild SOLUTION_PATH /t:Restore,Build /p:Configuration=Release /m
               exit $LASTEXITCODE
               """
            .Replace("MSBUILD_PATH", quotedMSBuildPath, StringComparison.Ordinal)
            .Replace("SOLUTION_PATH", quotedSolutionPath, StringComparison.Ordinal);
    }

    private static string QuotePowerShell(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
