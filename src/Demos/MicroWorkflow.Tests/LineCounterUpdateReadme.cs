using KbgSoft.LineCounter;
using System.Reflection;

namespace MicroWorkflow;

public class LineCounterUpdateReadme
{
    [Test]
    [Explicit]
    public void UpdateReadme()
    {
        string topPath = Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "..", "..", ".."));

        // code
        string sourcePath = Path.GetFullPath(Path.Combine(topPath, "src"));
        Console.WriteLine($"root: {sourcePath}");
        var sourceCounter = new LineCounting();
        var files = sourceCounter.GetFiles(sourcePath)
            .Where(x => !x.Contains("\\src\\Demos\\") && !x.Contains("DemoImplementations\\") || x.Contains(".Tests"));
        Console.WriteLine($"counting:\n{string.Join("\n", files)}");
        var sourceStats = sourceCounter.CountFiles(files);

        // doc
        var documentationCounter = new LineCounting();
        var documentationStats = documentationCounter.CountFiles(
            documentationCounter
            .GetFiles(topPath)
            .Where(x => x.EndsWith(".md")));

        // merge
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
