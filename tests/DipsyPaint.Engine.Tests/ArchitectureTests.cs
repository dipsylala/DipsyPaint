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

    [Fact]
    public void LayerPixels_AreReachableOnlyThroughThePixelAccessAbstraction()
    {
        var publicTypes = typeof(Document).Assembly.GetExportedTypes();
        var layerMembers = typeof(Layer).GetProperties().Select(p => p.PropertyType)
            .Concat(typeof(Layer).GetMethods().Select(m => m.ReturnType));

        Assert.All(publicTypes.Where(t => typeof(IPixelAccess).IsAssignableFrom(t)), t => Assert.True(t.IsInterface));
        Assert.DoesNotContain(layerMembers, t => t.IsArray || t.Name.Contains("Span") || t.Name.Contains("Memory"));
    }
}
