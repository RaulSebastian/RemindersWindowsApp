using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace RemindersApp;

public partial class MainWindow : Window
{
    private const string RemindersUrl = "https://www.icloud.com/reminders/";
    private string? webViewUserDataFolder;


    public MainWindow()
    {
        InitializeComponent();
        StateChanged += MainWindow_StateChanged;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        CleanupWebViewUserData();
    }
    
    [SuppressMessage("ReSharper", "AsyncVoidEventHandlerMethod", Justification = "WPF virtual override")]
    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        try
        {
            await InitializeWebView();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Failed to initialize:{Environment.NewLine}{exception.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task InitializeWebView()
    {
        webViewUserDataFolder = Path.Combine(Path.GetTempPath(), $"RemindersApp_{Guid.NewGuid()}");
        Directory.CreateDirectory(webViewUserDataFolder);

        var webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, webViewUserDataFolder);
        await WebView.EnsureCoreWebView2Async(webViewEnvironment);
    }

    private void CleanupWebViewUserData()
    {
        if (string.IsNullOrEmpty(webViewUserDataFolder) || !Directory.Exists(webViewUserDataFolder))
        {
            return;
        }

        try
        {
            Directory.Delete(webViewUserDataFolder, recursive: true);
        }
        catch
        {
            // ignored
        }
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

        var coreWebView2 = WebView.CoreWebView2;
        ConfigureWebView(coreWebView2);
        coreWebView2.Navigate(RemindersUrl);
    }

    private void ConfigureWebView(CoreWebView2 coreWebView2)
    {
        ConfigureWebViewSettings(coreWebView2.Settings);
        SubscribeToWebViewEvents(coreWebView2);
        InjectWebViewScripts(coreWebView2);
    }

    private void ConfigureWebViewSettings(CoreWebView2Settings settings)
    {
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
    }

    private void SubscribeToWebViewEvents(CoreWebView2 coreWebView2)
    {
        coreWebView2.NewWindowRequested += HandleNewWindowRequested;
        coreWebView2.SourceChanged += HandleSourceChanged;
        coreWebView2.DocumentTitleChanged += (_, _) =>
            Dispatcher.Invoke(() => Title = coreWebView2.DocumentTitle is { Length: > 0 } title ? title : "Reminders");
    }

    private void InjectWebViewScripts(CoreWebView2 coreWebView2)
    {
        coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(HideExternalAppLinksScript);
        coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(HideHeaderChromeScript);
    }

    private const string HideHeaderChromeScript = """
        (function () {
            const css = 'header { display: none !important; }';
            const style = document.createElement('style');
            style.textContent = css;
            document.addEventListener('DOMContentLoaded', () => document.head.appendChild(style));
        })();
        """;

    private const string HideExternalAppLinksScript = """
        (function () {
            const authHosts = ['idmsa.apple.com', 'appleid.apple.com', 'gsa.apple.com', 'icloud.com.cn'];

            function shouldHide(el) {
                const href = el.getAttribute('href');
                if (!href) return false;
                try {
                    const url = new URL(href, location.href);
                    const h = url.hostname.toLowerCase();
                    if (authHosts.some(a => h === a || h.endsWith('.' + a))) return false;
                    if ((h === 'icloud.com' || h.endsWith('.icloud.com'))
                            && url.pathname.startsWith('/reminders')) return false;
                    return true;
                } catch { return false; }
            }

            function hideOffAppLinks(root) {
                (root.querySelectorAll ? root : document).querySelectorAll('a[href]').forEach(a => {
                    if (shouldHide(a)) a.style.setProperty('display', 'none', 'important');
                });
            }

            hideOffAppLinks(document);

            new MutationObserver(mutations => {
                for (const m of mutations)
                    for (const node of m.addedNodes)
                        if (node.nodeType === 1) hideOffAppLinks(node);
            }).observe(document.documentElement, { childList: true, subtree: true });
        })();
        """;

    private void HandleSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        var sourceUrl = WebView.CoreWebView2.Source;

        Dispatcher.Invoke(() =>
        {
            if (!NavigationGuard.IsAllowedUrl(sourceUrl))
                WebView.CoreWebView2.Navigate(RemindersUrl);
        });
    }

    private void HandleNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
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

    private async void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        HideLoadingIndicators();
        await ReapplyWebViewScripts();
    }

    private void HideLoadingIndicators()
    {
        Dispatcher.Invoke(() =>
        {
            LoadingIndicator.Visibility = Visibility.Collapsed;
            StatusBar.Visibility = Visibility.Collapsed;
        });
    }

    private async Task ReapplyWebViewScripts()
    {
        await WebView.CoreWebView2.ExecuteScriptAsync(HideExternalAppLinksScript);
        await WebView.CoreWebView2.ExecuteScriptAsync(HideHeaderChromeScript);
    }



    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        ToggleWindowMaximization();

    private void Close_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleWindowMaximization() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void MainWindow_StateChanged(object? sender, EventArgs e) =>
        MaximizeIcon.Data = System.Windows.Media.Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M2,0 H9 V7 M0,2 H7 V9 H0 Z"
                : "M0,0 H9 V9 H0 Z");

    // WindowStyle=None requires manual WM_GETMINMAXINFO handling to avoid covering the taskbar
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr windowHandle, int messageType, IntPtr wordParam, IntPtr longParam, ref bool messageHandled)
    {
        const int GetMinMaxInfoMessage = 0x0024;
        if (messageType == GetMinMaxInfoMessage)
        {
            var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(longParam);
            ApplyMonitorConstraints(windowHandle, ref minMaxInfo);
            Marshal.StructureToPtr(minMaxInfo, longParam, true);
            messageHandled = true;
        }
        return IntPtr.Zero;
    }

    private void ApplyMonitorConstraints(IntPtr windowHandle, ref MINMAXINFO minMaxInfo)
    {
        var monitorHandle = NativeMethods.MonitorFromWindow(windowHandle, 0x00000002);
        if (monitorHandle == IntPtr.Zero)
            return;

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitorHandle, ref monitorInfo);

        minMaxInfo.ptMaxPosition = new POINT(
            monitorInfo.rcWork.Left - monitorInfo.rcMonitor.Left,
            monitorInfo.rcWork.Top - monitorInfo.rcMonitor.Top);

        minMaxInfo.ptMaxSize = new POINT(
            monitorInfo.rcWork.Right - monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);

        minMaxInfo.ptMaxTrackSize = minMaxInfo.ptMaxSize;

        // Respect WPF MinWidth and MinHeight
        var source = HwndSource.FromHwnd(windowHandle);
        if (source?.CompositionTarget == null)
        {
            return;
        }

        var matrix = source.CompositionTarget.TransformToDevice;
        minMaxInfo.ptMinTrackSize = new POINT(
            (int)(MinWidth * matrix.M11),
            (int)(MinHeight * matrix.M22));
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
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    }
}
