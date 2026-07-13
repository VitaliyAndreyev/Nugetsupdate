using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using ProjectManager.App.Models;

namespace ProjectManager.App.Services;

public sealed partial class SolutionAnalyzer
{
    public IReadOnlyList<ProjectNode> LoadProjects(string solutionPath)
    {
        var solutionDirectory = Path.GetDirectoryName(solutionPath)
            ?? throw new InvalidOperationException("Solution directory could not be detected.");

        return File.ReadAllLines(solutionPath)
            .Select(line => ProjectLineRegex().Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["path"].Value)
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new ProjectNode
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Path = path
                })
            .OrderBy(node => node.Name)
            .ToList();
    }

    [GeneratedRegex("""Project\("\{[^}]+\}"\)\s*=\s*"[^"]+",\s*"(?<path>[^"]+\.csproj)",""", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectLineRegex();
}
