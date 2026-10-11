namespace DipsyPaint.Engine.Tools;

/// <summary>
/// A pending tool whose content turns to any angle, as reference Paint's selections, shapes and text
/// do: dragging <see cref="FrameHandle.Rotate"/> turns it about its centre, and
/// <see cref="DocumentEditor.Rotate"/> (Custom rotate) turns it by a given angle.
/// </summary>
public interface IRotatableTool : IPendingTool
{
    /// <summary>
    /// Turns the pending content <paramref name="degrees"/> clockwise about its centre; its frame
    /// becomes the upright rectangle around the result. Returns the changed rectangle; empty when
    /// nothing is pending.
    /// </summary>
    IntRect Rotate(ToolContext context, double degrees);
}

/// <summary>
/// A drag of the rotate handle: the angle the pointer has turned about the content's centre since
/// the press, added to the content's angle at the press. Shift snaps the result to 15° steps.
/// </summary>
internal sealed class RotateDrag(double centreX, double centreY, PointerInput press, double startAngle)
{
    /// <summary>Snap step with Shift, in degrees.</summary>
    public const double SnapDegrees = 15;

    private readonly double _pressAngle = AngleOf(centreX, centreY, press.X, press.Y);

    public double CentreX => centreX;
    public double CentreY => centreY;

    /// <summary>The content's angle at the press.</summary>
    public double StartAngle => startAngle;

    /// <summary>The content's angle for the pointer at <paramref name="input"/>, in (−180°, 180°].</summary>
    public double AngleAt(PointerInput input)
    {
        var angle = startAngle + AngleOf(centreX, centreY, input.X, input.Y) - _pressAngle;
        if (input.Shift)
        {
            angle = Math.Round(angle / SnapDegrees) * SnapDegrees;
        }

        angle = Math.IEEERemainder(angle, 360);
        return angle == -180 ? 180 : angle;
    }

    // Clockwise on screen (y down), from the positive x axis.
    private static double AngleOf(double centreX, double centreY, double x, double y) =>
        Math.Atan2(y - centreY, x - centreX) * 180 / Math.PI;
}
