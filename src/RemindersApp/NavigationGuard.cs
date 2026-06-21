namespace RemindersApp;

internal static class NavigationGuard
{
    internal static readonly string[] AllowedHosts =
    [
        "icloud.com",
        "apple.com",
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
        return AllowedHosts.Any(allowed => host == allowed || host.EndsWith("." + allowed));
    }
}
