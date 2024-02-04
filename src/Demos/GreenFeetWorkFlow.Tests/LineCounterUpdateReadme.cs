using KbgSoft.LineCounter;
using NUnit.Framework;
using System.Reflection;
using System.Text.RegularExpressions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace GreenFeetWorkflow.Tests;

public class LineCounterUpdateReadme
{
    [Test]
    [Explicit]
    public void UpdateReadme()
    {
        string topPath = Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "..", "..", ".."));

        string sourcePath = Path.GetFullPath(Path.Combine(topPath, "src"));
        Console.WriteLine($"root: {sourcePath}");
        var sourceCounter = new LineCounting();
        var files = sourceCounter.GetFiles(sourcePath)
            .Where(x => !x.Contains("\\src\\Demos\\") && !x.Contains("DemoImplementations\\") || x.Contains(".Tests"));
        Console.WriteLine($"counting:\n{string.Join("\n", files)}");
        var sourceStats = sourceCounter.CountFiles(files);

        var documentationCounter = new LineCounting();
        var documentationStats = documentationCounter.CountFiles(
            documentationCounter
            .GetFiles(topPath)
            .Where(x => x.EndsWith(".md")));

        sourceStats.Add("Markdown",
            new Statistics()
            {
                DocumentationLines = documentationStats.FiletypeStat["Markdown"].DocumentationLines
                - sourceStats.FiletypeStat["Markdown"].DocumentationLines
            });

        string readmePath = Path.GetFullPath(Path.Combine(topPath, "README.md"));
        sourceCounter.ReplaceWebshieldsInFile(sourceStats, readmePath);
    }
}
