using System.Text.Json;
using System.Text.Json.Serialization;

namespace DipsyPaint.Settings;

/// <summary>
/// The per-user settings file's contents. Each feature adds its own section as a property; a
/// missing section means "use the defaults". Sections this version does not know (written by a
/// later version) are kept in <see cref="Other"/> so saving does not drop them.
/// </summary>
public sealed class UserSettings
{
    /// <summary>The command bar layout (ADR-0022), or null for the default layout.</summary>
    public CommandBarSettings? CommandBar { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; set; }
}

/// <summary>The command group ids docked to each window edge, in order.</summary>
public sealed class CommandBarSettings
{
    public List<string?>? Top { get; set; }

    public List<string?>? Bottom { get; set; }

    public List<string?>? Left { get; set; }

    public List<string?>? Right { get; set; }
}

/// <summary>Source-generated JSON metadata, so the settings file needs no reflection (NativeAOT, ADR-0018).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
