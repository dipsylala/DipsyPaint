using System.Reflection;

namespace DipsyPaint.Engine.Tests.Codecs;

public class CodecBoundaryTests
{
    /// <summary>ImageSharp stays inside the engine; callers only ever see engine pixels (ADR-0008).</summary>
    [Fact]
    public void PublicEngineApi_ExposesNoImageSharpTypes()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var exposed = typeof(Document).Assembly.GetExportedTypes().SelectMany(t => t.GetMembers(flags)).SelectMany(m => m switch
        {
            MethodInfo method => method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType),
            PropertyInfo property => [property.PropertyType],
            FieldInfo field => [field.FieldType],
            ConstructorInfo constructor => constructor.GetParameters().Select(p => p.ParameterType),
            _ => [],
        });

        Assert.DoesNotContain(exposed, t => t.FullName?.StartsWith("SixLabors", StringComparison.Ordinal) == true);
    }
}
