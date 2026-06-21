namespace RemindersApp;

internal static class NavigationGuard
{
    // These hosts handle sign-in and are allowed regardless of path
    internal static readonly string[] AuthHosts =
    [
        "idmsa.apple.com",
        "appleid.apple.com",
        "gsa.apple.com",
        "icloud.com.cn",
    ];

    internal static bool IsAllowedUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Allow non-http schemes used internally (e.g. about:blank, data:)
        if (uri.Scheme != "https" && uri.Scheme != "http")
            return true;

        var host = uri.Host.ToLowerInvariant();

        if (AuthHosts.Any(a => host == a || host.EndsWith("." + a)))
            return true;

        // Only allow the reminders section of iCloud, not the homepage or other apps
        if (host == "icloud.com" || host.EndsWith(".icloud.com"))
            return uri.AbsolutePath.StartsWith("/reminders", StringComparison.OrdinalIgnoreCase);

        return false;
    }
}
