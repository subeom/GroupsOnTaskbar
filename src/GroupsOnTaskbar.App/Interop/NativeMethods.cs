using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace GroupsOnTaskbar.App.Interop;

public static class NativeMethods
{
    public static PointInt32 GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PointInt32(point.X, point.Y);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;

        public int Y;
    }
}
