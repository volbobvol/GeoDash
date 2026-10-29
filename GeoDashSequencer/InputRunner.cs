using System.Runtime.InteropServices;

namespace GeoDashSequencer;

public static class InputRunner
{
    public static async Task RunAsync(IEnumerable<SequenceStep> steps, CancellationToken cancellationToken)
    {
        foreach (SequenceStep step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (step)
            {
                case DelayStep delay:
                    await Task.Delay(delay.Milliseconds, cancellationToken);
                    break;
                case KeyboardCommandStep command:
                    SendKeyboard(command.Key, false);
                    SendKeyboard(command.Key, true);
                    break;
                case MouseCommandStep command:
                    SendMouse(command);
                    break;
                case LoopStep loop:
                    for (int repetition = 0; repetition < loop.Repetitions; repetition++)
                        await RunAsync(loop.Steps, cancellationToken);
                    break;
            }
        }
    }

    private static void SendMouse(MouseCommandStep command)
    {
        uint down = command.Button == MouseButton.Left ? MouseEventLeftDown : MouseEventRightDown;
        uint up = command.Button == MouseButton.Left ? MouseEventLeftUp : MouseEventRightUp;
        if (command.Action != MouseAction.Up)
            SendMouse(down);
        if (command.Action != MouseAction.Down)
            SendMouse(up);
    }

    private static void SendKeyboard(Keys key, bool keyUp) => SendInput(1,
        [new Input { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = (ushort)key, Flags = keyUp ? KeyEventKeyUp : 0 } } }],
        Marshal.SizeOf<Input>());

    private static void SendMouse(uint flags) => SendInput(1,
        [new Input { Type = InputMouse, Data = new InputUnion { Mouse = new MouseInput { Flags = flags } } }],
        Marshal.SizeOf<Input>());

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

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