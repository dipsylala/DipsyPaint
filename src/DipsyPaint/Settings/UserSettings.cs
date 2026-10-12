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

    /// <summary>Crash-recovery snapshots (ADR-0013), or null for the defaults.</summary>
    public RecoverySettings? Recovery { get; set; }

    /// <summary>The View menu's display options (rulers, gridlines, status bar), or null for the defaults.</summary>
    public ViewSettings? View { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Other { get; set; }
}

/// <summary>Which display options the View menu shows; a null value means its default.</summary>
public sealed class ViewSettings
{
    public bool? Rulers { get; set; }

    public bool? Gridlines { get; set; }

    public bool? StatusBar { get; set; }
}

/// <summary>The command group ids docked to each window edge, in order, and the undocked groups.</summary>
public sealed class CommandBarSettings
{
    public List<string?>? Top { get; set; }

    public List<string?>? Bottom { get; set; }

    public List<string?>? Left { get; set; }

    public List<string?>? Right { get; set; }

    /// <summary>The undocked groups (ADR-0026), bottom to top; null in layouts saved by v0.2.0 and earlier.</summary>
    public List<UndockedGroupSettings?>? Undocked { get; set; }
}

/// <summary>An undocked command group and where it is.</summary>
public sealed class UndockedGroupSettings
{
    public string? Id { get; set; }

    /// <summary>The group's left edge, in device-independent pixels from the canvas area's.</summary>
    public double? X { get; set; }

    /// <summary>The group's top edge, in device-independent pixels from the canvas area's.</summary>
    public double? Y { get; set; }
}

/// <summary>Crash-recovery snapshot settings.</summary>
public sealed class RecoverySettings
{
    /// <summary>The default time between snapshots while there are unsaved changes: 2 minutes (ADR-0013).</summary>
    public const int DefaultSnapshotIntervalSeconds = 120;

    /// <summary>The shortest and longest intervals honoured; other values are clamped to them.</summary>
    public const int MinSnapshotIntervalSeconds = 15;

    /// <inheritdoc cref="MinSnapshotIntervalSeconds"/>
    public const int MaxSnapshotIntervalSeconds = 60 * 60;

    /// <summary>Seconds between snapshots while there are unsaved changes; null for the default.</summary>
    public int? SnapshotIntervalSeconds { get; set; }

    /// <summary>The interval to use: the setting clamped to the honoured range, or the default.</summary>
    public static TimeSpan IntervalOf(RecoverySettings? settings) => TimeSpan.FromSeconds(
        Math.Clamp(settings?.SnapshotIntervalSeconds ?? DefaultSnapshotIntervalSeconds, MinSnapshotIntervalSeconds, MaxSnapshotIntervalSeconds));
}

/// <summary>Source-generated JSON metadata, so the settings file needs no reflection (NativeAOT, ADR-0018).</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
