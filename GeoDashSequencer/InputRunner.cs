using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GeoDashSequencer;

public static partial class InputRunner
{
    public static async Task RunAsync(IEnumerable<SequenceStep> steps, CancellationToken cancellationToken, Action<SequenceStep>? stepStarted = null)
    {
        foreach (SequenceStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stepStarted?.Invoke(step);
            switch (step)
            {
                case DelayStep delay:
                    await Task.Delay(delay.Milliseconds, cancellationToken);
                    break;
                case WaitForPixelChangeStep wait:
                    await WaitForPixelChangeAsync(wait, cancellationToken);
                    break;
                case WaitForPixelStep wait:
                    await WaitForPixelAsync(wait, cancellationToken);
                    break;
                case KeyboardCommandStep command:
                    SendKeyboard(command.Key);
                    break;
                case MouseCommandStep command:
                    SendMouse(command);
                    break;
                case LoopStep loop:
                    for (int repetition = 0; repetition < loop.Repetitions; repetition++)
                        await RunAsync(loop.Steps, cancellationToken, stepStarted);
                    break;
            }
        }
    }

    private static async Task WaitForPixelChangeAsync(WaitForPixelChangeStep wait, CancellationToken cancellationToken)
    {
        uint initialColor = ScreenPixelReader.ReadColor(wait.ScreenX, wait.ScreenY);
        while (true)
        {
            await Task.Delay(wait.PollMilliseconds, cancellationToken);
            if (ScreenPixelReader.ReadColor(wait.ScreenX, wait.ScreenY) != initialColor)
                return;
        }
    }

    private static async Task WaitForPixelAsync(WaitForPixelStep wait, CancellationToken cancellationToken)
    {
        while (true)
        {
            Color currentColor = ScreenPixelReader.ToColor(ScreenPixelReader.ReadColor(wait.ScreenX, wait.ScreenY));
            if (GetNormalizedColorDistance(currentColor, wait.TargetColor) <= wait.Delta)
                return;
            await Task.Delay(wait.PollMilliseconds, cancellationToken);
        }
    }

    private static double GetNormalizedColorDistance(Color left, Color right)
    {
        int redDifference = left.R - right.R;
        int greenDifference = left.G - right.G;
        int blueDifference = left.B - right.B;
        return Math.Sqrt(redDifference * redDifference + greenDifference * greenDifference + blueDifference * blueDifference) / (255 * Math.Sqrt(3));
    }

    private static void SendMouse(MouseCommandStep command)
    {
        uint down = command.Button == MouseButton.Left ? MouseEventLeftDown : MouseEventRightDown;
        uint up = command.Button == MouseButton.Left ? MouseEventLeftUp : MouseEventRightUp;
        Input[] inputs = command.Action switch
        {
            MouseAction.Click => [CreateMouseInput(down), CreateMouseInput(up)],
            MouseAction.Down => [CreateMouseInput(down)],
            MouseAction.Up => [CreateMouseInput(up)],
            _ => throw new InvalidOperationException("The mouse command contains an invalid action.")
        };
        SendInputs(inputs);
    }

    private static void SendKeyboard(Keys shortcut)
    {
        List<Keys> modifiers = [];
        if (shortcut.HasFlag(Keys.Control))
            modifiers.Add(Keys.ControlKey);
        if (shortcut.HasFlag(Keys.Shift))
            modifiers.Add(Keys.ShiftKey);
        if (shortcut.HasFlag(Keys.Alt))
            modifiers.Add(Keys.Menu);

        List<Input> inputs = modifiers.Select(key => CreateKeyboardInput(key, false)).ToList();
        Keys keyCode = shortcut & Keys.KeyCode;
        inputs.Add(CreateKeyboardInput(keyCode, false));
        inputs.Add(CreateKeyboardInput(keyCode, true));
        for (int index = modifiers.Count - 1; index >= 0; index--)
            inputs.Add(CreateKeyboardInput(modifiers[index], true));
        SendInputs([.. inputs]);
    }

    private static Input CreateKeyboardInput(Keys key, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = (ushort)key, Flags = keyUp ? KeyEventKeyUp : 0 } }
    };

    private static Input CreateMouseInput(uint flags) => new()
    {
        Type = InputMouse,
        Data = new InputUnion { Mouse = new MouseInput { Flags = flags } }
    };

    private static void SendInputs(params Input[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != (uint)inputs.Length)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Windows rejected one or more simulated inputs.");
    }

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}