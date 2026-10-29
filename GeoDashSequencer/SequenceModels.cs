using System.ComponentModel;

namespace GeoDashSequencer;

public abstract class SequenceStep
{
    [Browsable(false)]
    public abstract string Summary { get; }
}

public sealed class DelayStep : SequenceStep
{
    private int _milliseconds = 100;

    [DisplayName("Milliseconds")]
    [Description("Time to wait before the next step.")]
    public int Milliseconds
    {
        get => _milliseconds;
        set => _milliseconds = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Milliseconds must be non-negative.");
    }

    public override string Summary => $"Delay {Milliseconds} ms";
}

public sealed class WaitForPixelChangeStep : SequenceStep
{
    private int _pollMilliseconds = 50;

    [DisplayName("Screen X")]
    [Description("Absolute horizontal screen coordinate to watch.")]
    public int ScreenX { get; set; }

    [DisplayName("Screen Y")]
    [Description("Absolute vertical screen coordinate to watch.")]
    public int ScreenY { get; set; }

    [DisplayName("Poll milliseconds")]
    [Description("How often the pixel is sampled while waiting for a change.")]
    public int PollMilliseconds
    {
        get => _pollMilliseconds;
        set => _pollMilliseconds = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Poll milliseconds must be greater than zero.");
    }

    public override string Summary => $"Wait for pixel change at {ScreenX}, {ScreenY}";
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

    public override string Summary => $"Key press: {KeyboardShortcut.Format(Key)}";
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
    private int _repetitions = 2;

    [DisplayName("Repetitions")]
    public int Repetitions
    {
        get => _repetitions;
        set => _repetitions = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Repetitions must be non-negative.");
    }

    [Browsable(false)]
    public List<SequenceStep> Steps { get; } = [];

    public override string Summary => $"Loop {Repetitions} times";
}

internal static class SequenceValidator
{
    private const int MaximumLoopDepth = 32;

    public static List<SequenceStep> CreateExecutionSnapshot(IEnumerable<SequenceStep> steps) => CloneSteps(steps, 0);

    private static List<SequenceStep> CloneSteps(IEnumerable<SequenceStep> steps, int depth)
    {
        List<SequenceStep> snapshot = [];
        foreach (SequenceStep step in steps)
        {
            snapshot.Add(step switch
            {
                DelayStep delay => new DelayStep { Milliseconds = delay.Milliseconds },
                WaitForPixelChangeStep wait => new WaitForPixelChangeStep { ScreenX = wait.ScreenX, ScreenY = wait.ScreenY, PollMilliseconds = wait.PollMilliseconds },
                KeyboardCommandStep keyboard when KeyboardShortcut.IsValid(keyboard.Key) => new KeyboardCommandStep { Key = keyboard.Key },
                KeyboardCommandStep => throw new InvalidOperationException("A keyboard command contains an invalid key."),
                MouseCommandStep mouse when Enum.IsDefined(mouse.Button) && Enum.IsDefined(mouse.Action) => new MouseCommandStep { Button = mouse.Button, Action = mouse.Action },
                MouseCommandStep => throw new InvalidOperationException("A mouse command contains an invalid button or action."),
                LoopStep loop when depth < MaximumLoopDepth => CloneLoop(loop, depth),
                LoopStep => throw new InvalidOperationException($"Loop nesting cannot exceed {MaximumLoopDepth} levels."),
                _ => throw new InvalidOperationException("The sequence contains an unsupported step.")
            });
        }
        return snapshot;
    }

    private static LoopStep CloneLoop(LoopStep loop, int depth)
    {
        LoopStep clone = new() { Repetitions = loop.Repetitions };
        clone.Steps.AddRange(CloneSteps(loop.Steps, depth + 1));
        return clone;
    }
}

internal static class KeyboardShortcut
{
    private const Keys SupportedModifiers = Keys.Shift | Keys.Control | Keys.Alt;

    public static bool IsValid(Keys shortcut)
    {
        int value = (int)shortcut;
        int keyCode = value & (int)Keys.KeyCode;
        int unsupportedBits = value & ~((int)Keys.KeyCode | (int)SupportedModifiers);
        return keyCode != 0 && Enum.IsDefined((Keys)keyCode) && unsupportedBits == 0;
    }

    public static string Format(Keys shortcut)
    {
        List<string> parts = [];
        if (shortcut.HasFlag(Keys.Control))
            parts.Add("Ctrl");
        if (shortcut.HasFlag(Keys.Shift))
            parts.Add("Shift");
        if (shortcut.HasFlag(Keys.Alt))
            parts.Add("Alt");
        parts.Add((shortcut & Keys.KeyCode).ToString());
        return string.Join('+', parts);
    }

    public static Keys Parse(string value)
    {
        Keys shortcut = Keys.None;
        foreach (string part in value.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Keys key = part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                ? Keys.Control
                : Enum.TryParse(part, true, out Keys parsed)
                    ? parsed
                    : throw new FormatException($"Invalid key '{part}'.");
            shortcut |= key;
        }

        if (!IsValid(shortcut))
            throw new FormatException("The shortcut must include a valid primary key.");
        return shortcut;
    }
}