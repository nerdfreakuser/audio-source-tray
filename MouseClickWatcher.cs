using System.Runtime.InteropServices;

namespace AudioSourceTray;

sealed class MouseClickWatcher : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;

    private readonly SynchronizationContext _sync;
    private readonly Action<Point> _onClick;
    private readonly HookProc _proc;
    private IntPtr _hook;

    public MouseClickWatcher(Action<Point> onClick)
    {
        _onClick = onClick;
        _sync = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = SetWindowsHookEx(WhMouseLl, _proc, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            if (msg is WmLButtonDown or WmRButtonDown or WmMButtonDown)
            {
                var info = Marshal.PtrToStructure<MsllHookStruct>(lParam);
                var point = new Point(info.Point.X, info.Point.Y);
                _sync.Post(_ => _onClick(point), null);
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public PointStruct Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
