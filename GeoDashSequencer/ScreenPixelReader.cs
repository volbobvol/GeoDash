using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GeoDashSequencer;

internal static partial class ScreenPixelReader
{
    private const uint InvalidColor = 0xFFFFFFFF;

    public static uint ReadColor(int x, int y)
    {
        nint deviceContext = GetDC(nint.Zero);
        if (deviceContext == nint.Zero)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not access the desktop pixels.");

        try
        {
            uint color = GetPixel(deviceContext, x, y);
            if (color == InvalidColor)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not read the selected screen pixel.");
            return color;
        }
        finally
        {
            ReleaseDC(nint.Zero, deviceContext);
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint GetDC(nint window);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int ReleaseDC(nint window, nint deviceContext);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial uint GetPixel(nint deviceContext, int x, int y);
}