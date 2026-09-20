using System.Runtime.InteropServices;

namespace AudioSourceTray;

sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _startupItem;
    private readonly AudioMonitor _monitor = new();
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly System.Windows.Forms.Timer _hideTimer;
    private readonly System.Windows.Forms.Timer _promoteTimer;
    private readonly MouseClickWatcher _clickWatcher;
    private HoverPopup? _popup;
    private AudioSnapshot _snapshot = AudioSnapshot.Empty;
    private Point _lastHoverPoint;
    private bool _playingIcon;
    private bool _pinned;
    private bool _menuOpen;

    public TrayApplicationContext()
    {
        StartupManager.EnableOnFirstRun();

        _pollTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _hideTimer = new System.Windows.Forms.Timer { Interval = 80 };
        _promoteTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _clickWatcher = new MouseClickWatcher(OnOutsideClick);

        _startupItem = new ToolStripMenuItem("Run on startup")
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled(),
        };
        _startupItem.CheckedChanged += (_, _) => StartupManager.SetEnabled(_startupItem.Checked);

        _menu = new ContextMenuStrip { AutoClose = true };
        _menu.Items.Add(_startupItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Close", null, (_, _) => ExitThread());
        _menu.Opening += (_, _) => SyncStartupItem();
        _menu.Closed += (_, _) =>
        {
            _menuOpen = false;
            if (_popup is not { Visible: true })
            {
                _clickWatcher.Stop();
            }
        };

        _tray = new NotifyIcon
        {
            Icon = AppIcons.Idle,
            Visible = true,
            Text = "",
        };
        _tray.MouseMove += (_, _) => OnTrayHover();
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                PinAndShow();
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowContextMenu();
            }
        };

        _pollTimer.Tick += (_, _) => Poll();
        _hideTimer.Tick += (_, _) => MaybeHide();
        _promoteTimer.Tick += (_, _) =>
        {
            _promoteTimer.Stop();
            TrayIconPromoter.PromoteThisApp();
        };

        TrayIconPromoter.PromoteThisApp();
        _pollTimer.Start();
        _promoteTimer.Start();
        Poll();
    }

    private HoverPopup Popup
    {
        get
        {
            if (_popup is null)
            {
                _popup = new HoverPopup();
                _popup.MouseEnter += (_, _) => _hideTimer.Stop();
                _popup.MouseLeave += (_, _) => ScheduleHide();
            }

            return _popup;
        }
    }

    private void Poll()
    {
        try
        {
            var snapshot = _monitor.Capture();
            _snapshot = snapshot;

            if (_popup is { Visible: true })
            {
                _popup.SetSnapshot(snapshot);
                _popup.Invalidate();
            }

            if (snapshot.IsPlaying != _playingIcon)
            {
                _playingIcon = snapshot.IsPlaying;
                _tray.Icon = snapshot.IsPlaying ? AppIcons.Playing : AppIcons.Idle;
            }
        }
        catch
        {
            // Keep the tray alive if a single poll fails.
        }
    }

    private void SyncStartupItem()
    {
        var enabled = StartupManager.IsEnabled();
        if (_startupItem.Checked != enabled)
        {
            _startupItem.Checked = enabled;
        }
    }

    private void ShowContextMenu()
    {
        _menuOpen = true;
        HidePopup();
        SyncStartupItem();
        _menu.Show(Cursor.Position);
        if (_menu.IsHandleCreated)
        {
            SetForegroundWindow(_menu.Handle);
        }

        _clickWatcher.Start();
    }

    private void OnTrayHover()
    {
        if (_menuOpen || _menu.Visible)
        {
            return;
        }

        ShowPopup();
        if (!_pinned)
        {
            _hideTimer.Start();
        }
    }

    private void PinAndShow()
    {
        if (_menuOpen || _menu.Visible)
        {
            return;
        }

        _pinned = true;
        _hideTimer.Stop();
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_menuOpen || _menu.Visible)
        {
            return;
        }

        _lastHoverPoint = Cursor.Position;
        Popup.SetSnapshot(_snapshot);
        Popup.ShowNear(_lastHoverPoint);
        _clickWatcher.Start();
    }

    private void ScheduleHide()
    {
        if (_pinned)
        {
            return;
        }

        _hideTimer.Start();
    }

    private void MaybeHide()
    {
        if (_pinned)
        {
            return;
        }

        if (_popup is not { Visible: true })
        {
            _hideTimer.Stop();
            return;
        }

        var cursor = Cursor.Position;
        if (_popup.ContainsScreenPoint(cursor))
        {
            return;
        }

        if (TrayIconBounds.ContainsCursor(_tray, cursor, _lastHoverPoint))
        {
            return;
        }

        HidePopup();
    }

    private void OnOutsideClick(Point screenPoint)
    {
        if (_menuOpen || _menu.Visible)
        {
            if (MenuContains(screenPoint))
            {
                return;
            }

            _menu.Close();
            _menuOpen = false;
            return;
        }

        if (_popup is not { Visible: true })
        {
            return;
        }

        if (_popup.ContainsScreenPoint(screenPoint))
        {
            return;
        }

        if (TrayIconBounds.ContainsCursor(_tray, screenPoint, _lastHoverPoint))
        {
            return;
        }

        HidePopup();
    }

    private void HidePopup()
    {
        _pinned = false;
        _clickWatcher.Stop();
        _hideTimer.Stop();
        _popup?.HidePopup();
    }

    private bool MenuContains(Point screenPoint)
    {
        if (!_menu.Visible || !_menu.IsHandleCreated)
        {
            return false;
        }

        var rect = _menu.RectangleToScreen(_menu.ClientRectangle);
        rect.Inflate(6, 6);
        return rect.Contains(screenPoint);
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override void ExitThreadCore()
    {
        _pollTimer.Stop();
        _hideTimer.Stop();
        _promoteTimer.Stop();
        _clickWatcher.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _popup?.Dispose();
        _monitor.Dispose();
        base.ExitThreadCore();
    }
}
