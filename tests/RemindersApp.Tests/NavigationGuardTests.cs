using RemindersApp;
using Xunit;

namespace RemindersApp.Tests;

public class NavigationGuardTests
{
    [Theory]
    [InlineData("https://www.icloud.com/reminders/")]
    [InlineData("https://icloud.com/reminders/")]
    [InlineData("https://icloud.com/reminders/some/deep/path")]
    [InlineData("https://idmsa.apple.com/appleauth/auth")]
    [InlineData("https://appleid.apple.com/account/login")]
    [InlineData("https://gsa.apple.com/grandslam/GsService2")]
    public void IsAllowedUrl_RemindersAndAuthUrls_ReturnsTrue(string url)
    {
        Assert.True(NavigationGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("https://www.icloud.com/")]
    [InlineData("https://icloud.com/")]
    [InlineData("https://www.icloud.com/photos/")]
    [InlineData("https://www.apple.com/reminders/")]
    [InlineData("https://evil.com/")]
    [InlineData("https://icloud.com.evil.com/")]
    [InlineData("https://fakeicloud.com/")]
    public void IsAllowedUrl_NonRemindersUrls_ReturnsFalse(string url)
    {
        Assert.False(NavigationGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("about:blank")]
    [InlineData("data:text/html,<html></html>")]
    public void IsAllowedUrl_InternalScheme_ReturnsTrue(string url)
    {
        Assert.True(NavigationGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    public void IsAllowedUrl_InvalidUrl_ReturnsFalse(string url)
    {
        Assert.False(NavigationGuard.IsAllowedUrl(url));
    }
}
