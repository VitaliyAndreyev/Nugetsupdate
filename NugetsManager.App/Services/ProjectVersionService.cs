using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NugetsManager.App.Models;

namespace NugetsManager.App.Services;

public sealed class ProjectVersionService
{
    private static readonly string[] VersionProperties =
    [
        "Version",
        "FileVersion",
        "AssemblyVersion"
    ];

    public string ReadVersion(ProjectNode project)
    {
        if (!File.Exists(project.Path))
        {
            return string.Empty;
        }

        var document = XDocument.Load(project.Path);
        return FindProperty(document, "FileVersion")
            ?? FindProperty(document, "Version")
            ?? FindProperty(document, "AssemblyVersion")
            ?? string.Empty;
    }

    public void WriteVersion(ProjectNode project, string version)
    {
        var content = File.ReadAllText(project.Path);
        var updated = content;

        foreach (var propertyName in VersionProperties)
        {
            updated = ReplaceExistingProperty(updated, propertyName, version);
        }

        if (updated == content)
        {
            WriteVersionWithXmlFallback(project, version);
        }
        else
        {
            File.WriteAllText(project.Path, updated);
            project.ProjectVersion = version;
        }
    }

    private static string ReplaceExistingProperty(string content, string propertyName, string version)
    {
        return Regex.Replace(
            content,
            $@"(<{propertyName}>)(.*?)(</{propertyName}>)",
            match => $"{match.Groups[1].Value}{version}{match.Groups[3].Value}",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }

    private static void WriteVersionWithXmlFallback(ProjectNode project, string version)
    {
        var document = XDocument.Load(project.Path, LoadOptions.PreserveWhitespace);
        var root = document.Root ?? throw new InvalidOperationException($"Project file {project.Path} does not have a root element.");
        var propertyGroup = root
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "PropertyGroup" && !element.HasAttributes);

        if (propertyGroup is null)
        {
            propertyGroup = new XElement(root.GetDefaultNamespace() + "PropertyGroup");
            root.AddFirst(propertyGroup);
        }

        foreach (var propertyName in VersionProperties)
        {
            SetProperty(propertyGroup, propertyName, version);
        }

        document.Save(project.Path);
        project.ProjectVersion = version;
    }

    private static string? FindProperty(XDocument document, string propertyName)
        => document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == propertyName)
            ?.Value;

    private static void SetProperty(XElement propertyGroup, string propertyName, string value)
    {
        var existing = propertyGroup.Elements().FirstOrDefault(element => element.Name.LocalName == propertyName);
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        propertyGroup.Add(new XElement(propertyGroup.Name.Namespace + propertyName, value));
    }
}
