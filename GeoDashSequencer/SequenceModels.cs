using System.ComponentModel;

namespace GeoDashSequencer;

public abstract class SequenceStep
{
    [Browsable(false)]
    public abstract string Summary { get; }
}

public sealed class DelayStep : SequenceStep
{
    [DisplayName("Milliseconds")]
    [Description("Time to wait before the next step.")]
    public int Milliseconds { get; set; } = 100;

    public override string Summary => $"Delay {Milliseconds} ms";
}

public enum MouseButton
{
    Left,
    Right
}

public enum MouseAction
{
    Click,
    Down,
    Up
}

public sealed class KeyboardCommandStep : SequenceStep
{
    [DisplayName("Key")]
    [Description("Virtual key to press.")]
    public Keys Key { get; set; } = Keys.Space;

    public override string Summary => $"Key press: {Key}";
}

public sealed class MouseCommandStep : SequenceStep
{
    [DisplayName("Mouse button")]
    public MouseButton Button { get; set; } = MouseButton.Left;

    [DisplayName("Mouse action")]
    public MouseAction Action { get; set; } = MouseAction.Click;

    public override string Summary => $"Mouse {Button} {Action}";
}

public sealed class LoopStep : SequenceStep
{
    [DisplayName("Repetitions")]
    public int Repetitions { get; set; } = 2;

    [Browsable(false)]
    public List<SequenceStep> Steps { get; } = [];

    public override string Summary => $"Loop {Repetitions} times";
}