using System.Reflection;
using System.Runtime.InteropServices;

namespace AudioSourceTray;

static class TrayIconBounds
{
    public static bool ContainsCursor(NotifyIcon icon, Point cursor, Point lastHoverPoint)
    {
        if (TryGetIconRect(icon, out var rect))
        {
            rect.Inflate(10, 14);
            if (rect.Contains(cursor))
            {
                return true;
            }
        }

        const int fallbackPad = 22;
        var fallback = new Rectangle(
            lastHoverPoint.X - fallbackPad,
            lastHoverPoint.Y - fallbackPad,
            fallbackPad * 2,
            fallbackPad * 2);
        return fallback.Contains(cursor);
    }

    public static bool TryGetIconRect(NotifyIcon icon, out Rectangle rect)
    {
        rect = Rectangle.Empty;
        try
        {
            var type = typeof(NotifyIcon);
            var window = GetField(type, icon, "window", "_window");
            var idObj = GetField(type, icon, "id", "_id");
            if (window is null || idObj is null)
            {
                return false;
            }

            IntPtr hwnd;
            if (window is NativeWindow native)
            {
                hwnd = native.Handle;
            }
            else
            {
                hwnd = window.GetType().GetProperty("Handle")?.GetValue(window) is IntPtr handle
                    ? handle
                    : IntPtr.Zero;
            }
            var id = Convert.ToUInt32(idObj);
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            var identifier = new NotifyIconIdentifier
            {
                CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
                Hwnd = hwnd,
                Uid = id,
                GuidItem = Guid.Empty,
            };

            if (Shell_NotifyIconGetRect(ref identifier, out var nativeRect) != 0)
            {
                return false;
            }

            rect = Rectangle.FromLTRB(nativeRect.Left, nativeRect.Top, nativeRect.Right, nativeRect.Bottom);
            return rect.Width > 0 && rect.Height > 0;
        }
        catch
        {
            return false;
        }
    }

    private static object? GetField(Type type, object instance, params string[] names)
    {
        foreach (var name in names)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                return field.GetValue(instance);
            }
        }

        return null;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out NativeRect iconLocation);

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public IntPtr Hwnd;
        public uint Uid;
        public Guid GuidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
