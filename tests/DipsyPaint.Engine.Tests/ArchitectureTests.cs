using DipsyPaint.Engine;

namespace DipsyPaint.Engine.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Engine_DoesNotReferenceAvalonia()
    {
        var references = typeof(Document).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => r.Name!.StartsWith("Avalonia", StringComparison.Ordinal));
    }
}
