using System.Reflection;
using KbgSoft.LineCounter;

namespace GreenFeetWorkflow.Tests;

public class LineCounterUpdateReadme
{
    [Test]
    public void UpdateReadme()
    {
        var basePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "..","..");
        Console.WriteLine(basePath);
        var linecounter = new LineCounting();
        linecounter.ReplaceWebshieldsInFile(basePath, Path.Combine(basePath, "README.md"));
    }
}
