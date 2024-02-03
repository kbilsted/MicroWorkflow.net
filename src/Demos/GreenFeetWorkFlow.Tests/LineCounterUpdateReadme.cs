using KbgSoft.LineCounter;
using System.Reflection;

namespace GreenFeetWorkflow.Tests;

public class LineCounterUpdateReadme
{
    [Test]
    public void UpdateReadme()
    {
        var basePath = Path.Combine(Assembly.GetExecutingAssembly().Location, "..", "..", "..", "..", "..", "..","..");
        Console.WriteLine(basePath);
        var linecounter = new LineCounting();
        string destinationFile = Path.Combine(basePath, "README.md");
        linecounter.ReplaceWebshieldsInFile(basePath, destinationFile);
    }
}
