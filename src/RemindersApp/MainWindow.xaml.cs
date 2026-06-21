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


    public MainWindow()
    {
        InitializeComponent();
        StateChanged += MainWindow_StateChanged;
        InitWebView();
    }

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
        ConfigureWebView(core);
        core.Navigate(RemindersUrl);
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsGeneralAutofillEnabled = true;
        core.Settings.IsPasswordAutosaveEnabled = true;

        core.NewWindowRequested += Core_NewWindowRequested;
        core.DocumentTitleChanged += (_, _) =>
            Dispatcher.Invoke(() => Title = core.DocumentTitle is { Length: > 0 } t ? t : "Reminders");
    }

    private void Core_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (NavigationGuard.IsAllowedUrl(e.Uri))
            WebView.CoreWebView2.Navigate(e.Uri);
    }

    private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!NavigationGuard.IsAllowedUrl(e.Uri))
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

    private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            StatusBar.Visibility = Visibility.Collapsed;
        });
    }


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
        MaximizeIcon.Data = System.Windows.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M2,0 H9 V7 M0,2 H7 V9 H0 Z"
                : "M0,0 H9 V9 H0 Z");
    }

    // WindowStyle=None requires manual WM_GETMINMAXINFO handling to avoid covering the taskbar
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = NativeMethods.MonitorFromWindow(hwnd, 0x00000002);
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

    // WindowStyle=None means WPF won't handle top-edge resize; SC_SIZE sends it to the OS
    private void TopResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SendMessage(handle, 0x0112, (IntPtr)0xF003, IntPtr.Zero);
    }

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
