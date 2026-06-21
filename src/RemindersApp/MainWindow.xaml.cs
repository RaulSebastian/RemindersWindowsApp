using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace RemindersApp;

public partial class MainWindow : Window
{
    private const string RemindersUrl = "https://www.icloud.com/reminders/";

    // Allowed host patterns (navigation stays within these)
    private static readonly string[] AllowedHosts =
    [
        "icloud.com",
        "apple.com",
        "idmsa.apple.com",
        "appleid.apple.com",
        "gsa.apple.com",
        "icloud.com.cn",
    ];

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += MainWindow_StateChanged;
        InitWebView();
    }

    // ── WebView setup ─────────────────────────────────────────────────────────

    private async void InitWebView()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RemindersApp", "WebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);
    }

    private void WebView_InitializationCompleted(object sender,
        CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            MessageBox.Show($"WebView2 failed to initialize:\n{e.InitializationException?.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var core = WebView.CoreWebView2;

        // Remove default context menus and dev tools shortcut
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        // Suppress "open in external browser" prompts
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsGeneralAutofillEnabled = true;
        core.Settings.IsPasswordAutosaveEnabled = true;

        // Block new windows (links that open in a new tab etc.)
        core.NewWindowRequested += Core_NewWindowRequested;

        // Handle favicon / title updates
        core.DocumentTitleChanged += (_, _) =>
        {
            Dispatcher.Invoke(() => Title = core.DocumentTitle is { Length: > 0 } t ? t : "Reminders");
        };

        core.Navigate(RemindersUrl);
    }

    private void Core_NewWindowRequested(object? sender,
        CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Instead of opening a new window, navigate in place if it's an allowed URL
        e.Handled = true;
        if (IsAllowedUrl(e.Uri))
            WebView.CoreWebView2.Navigate(e.Uri);
    }

    // ── Navigation guard ──────────────────────────────────────────────────────

    private void WebView_NavigationStarting(object sender,
        CoreWebView2NavigationStartingEventArgs e)
    {
        if (!IsAllowedUrl(e.Uri))
        {
            e.Cancel = true;
            return;
        }

        Dispatcher.Invoke(() =>
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            StatusBar.Visibility = Visibility.Visible;
            StatusText.Text = $"Loading {new Uri(e.Uri).Host}…";
        });
    }

    private void WebView_NavigationCompleted(object sender,
        CoreWebView2NavigationCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            StatusBar.Visibility = Visibility.Collapsed;
        });
    }

    private static bool IsAllowedUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Allow non-http schemes used internally (e.g. about:blank, data:)
        if (uri.Scheme != "https" && uri.Scheme != "http")
            return true;

        var host = uri.Host.ToLowerInvariant();
        return AllowedHosts.Any(allowed => host == allowed || host.EndsWith("." + allowed));
    }

    // ── Window chrome ─────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Update the maximize/restore button icon
        MaximizeIcon.Data = System.Windows.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M2,0 H9 V7 M0,2 H7 V9 H0 Z"   // restore icon
                : "M0,0 H9 V9 H0 Z");              // maximize icon
    }

    // Handle WM_GETMINMAXINFO so the window doesn't overlap the taskbar when maximized
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
        ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = NativeMethods.MonitorFromWindow(hwnd, 0x00000002 /* MONITOR_DEFAULTTONEAREST */);
            if (monitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                NativeMethods.GetMonitorInfo(monitor, ref info);
                mmi.ptMaxPosition = new POINT(
                    info.rcWork.Left - info.rcMonitor.Left,
                    info.rcWork.Top - info.rcMonitor.Top);
                mmi.ptMaxSize = new POINT(
                    info.rcWork.Right - info.rcWork.Left,
                    info.rcWork.Bottom - info.rcWork.Top);
                mmi.ptMaxTrackSize = mmi.ptMaxSize;
            }
            Marshal.StructureToPtr(mmi, lParam, true);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // Top edge resize (WindowStyle=None means we need to handle it)
    private void TopResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SendMessage(handle, 0x0112 /* WM_SYSCOMMAND */,
            (IntPtr)0xF003 /* SC_SIZE | WMSZ_TOP */, IntPtr.Zero);
    }

    // ── Native structs/methods ────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT(int x, int y)
    {
        public int X = x, Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}
