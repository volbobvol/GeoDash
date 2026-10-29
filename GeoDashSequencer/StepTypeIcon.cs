using System.Drawing.Drawing2D;

namespace GeoDashSequencer;

internal sealed class StepTypeIcon : Control
{
    private readonly SequenceStep _step;
    private readonly Color _color;

    public StepTypeIcon(SequenceStep step, Color color)
    {
        _step = step;
        _color = color;
        Size = new Size(22, 22);
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen pen = new(_color, 2);
        using Brush brush = new SolidBrush(_color);
        Rectangle bounds = new(3, 3, Width - 7, Height - 7);

        switch (_step)
        {
            case DelayStep:
                eventArgs.Graphics.DrawEllipse(pen, bounds);
                eventArgs.Graphics.DrawLine(pen, Width / 2, Height / 2, Width / 2, 6);
                eventArgs.Graphics.DrawLine(pen, Width / 2, Height / 2, Width - 6, Height / 2);
                break;
            case WaitForPixelChangeStep:
                eventArgs.Graphics.DrawRectangle(pen, bounds);
                eventArgs.Graphics.FillRectangle(brush, Width / 2 - 2, Height / 2 - 2, 4, 4);
                break;
            case KeyboardCommandStep:
                eventArgs.Graphics.DrawRectangle(pen, bounds);
                for (int row = 0; row < 2; row++)
                    for (int column = 0; column < 3; column++)
                        eventArgs.Graphics.FillRectangle(brush, 6 + column * 5, 6 + row * 5, 3, 3);
                break;
            case MouseCommandStep:
                eventArgs.Graphics.DrawEllipse(pen, bounds);
                eventArgs.Graphics.DrawLine(pen, Width / 2, 4, Width / 2, Height - 4);
                eventArgs.Graphics.DrawLine(pen, 5, Height / 2, Width - 5, Height / 2);
                break;
            case LoopStep:
                eventArgs.Graphics.DrawArc(pen, bounds, 45, 270);
                eventArgs.Graphics.DrawLine(pen, Width - 5, 6, Width - 5, 11);
                eventArgs.Graphics.DrawLine(pen, Width - 5, 6, Width - 10, 6);
                break;
        }
    }
}