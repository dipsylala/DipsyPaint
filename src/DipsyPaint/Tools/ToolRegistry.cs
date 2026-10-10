using Avalonia.Input;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// The tools offered in the command bar, in display order. To add a tool, add its descriptor
/// here; the command bar builds its buttons from this list. The first tool is selected at start.
/// </summary>
public static class ToolRegistry
{
    // Icons: Fluent UI System Icons (MIT licence, © Microsoft Corporation), ADR-0012.
    private const string EditIcon =
        "M20.95 3.05a3.6 3.6 0 0 0-5.06 0L3.94 15c-.4.4-.7.92-.82 1.48l-1.1 4.6a.75.75 0 0 0 .9.9l4.6-1.1A3 3 0 0 0 9 20.07L20.95 8.11a3.6 3.6 0 0 0 0-5.06m-4 1.06a2.08 2.08 0 1 1 2.94 2.94l-.89.89L16.06 5zM15 6.06 17.94 9l-10 10a1.6 1.6 0 0 1-.76.43l-3.42.8.82-3.4q.1-.45.42-.77z";

    private const string PaintBucketIcon =
        "M12 2.25C12 1.83579 11.6642 1.5 11.25 1.5C10.8358 1.5 10.5 1.83579 10.5 2.25V3.49946C10.1929 3.60776 9.90465 3.78472 9.65903 4.03035L2.78035 10.909C1.90167 11.7877 1.90167 13.2123 2.78035 14.091L7.65903 18.9697C8.53771 19.8484 9.96233 19.8484 10.841 18.9697L17.7197 12.091C18.5984 11.2123 18.5984 9.78771 17.7197 8.90903L12.841 4.03035C12.5954 3.78473 12.3071 3.60777 12 3.49947V2.25ZM3.84101 11.9697L10.5 5.31067V6.75C10.5 7.16421 10.8358 7.5 11.25 7.5C11.6642 7.5 12 7.16421 12 6.75V5.31069L16.659 9.96969C16.9519 10.2626 16.9519 10.7375 16.659 11.0303L15.6894 12H3.81231C3.82154 11.9897 3.83111 11.9796 3.84101 11.9697ZM4.31066 13.5H14.1894L9.78035 17.909C9.48745 18.2019 9.01258 18.2019 8.71969 17.909L4.31066 13.5ZM19.5212 13.6022C19.1922 12.9853 18.3079 12.9853 17.9788 13.6022L15.9706 17.3677C14.8516 19.4659 16.372 22 18.75 22C21.128 22 22.6485 19.4659 21.5294 17.3677L19.5212 13.6022ZM17.2941 18.0735L18.75 15.3438L20.2059 18.0735C20.7921 19.1726 19.9956 20.5 18.75 20.5C17.5044 20.5 16.708 19.1726 17.2941 18.0735Z";

    public static IReadOnlyList<ToolDescriptor> Tools { get; } =
    [
        new("pencil", ToolStrings.Pencil, EditIcon, new KeyGesture(Key.P), () => new PencilTool()),
        new("fill", ToolStrings.Fill, PaintBucketIcon, null, () => new FillTool()),
    ];
}
