using RemindersApp;
using Xunit;

namespace RemindersApp.Tests;

public class NavigationGuardTests
{
    [Theory]
    [InlineData("https://www.icloud.com/reminders/")]
    [InlineData("https://icloud.com/reminders/")]
    [InlineData("https://idmsa.apple.com/appleauth/auth")]
    [InlineData("https://appleid.apple.com/account/login")]
    [InlineData("https://gsa.apple.com/grandslam/GsService2")]
    [InlineData("https://sub.icloud.com/")]
    [InlineData("https://sub.apple.com/")]
    public void IsAllowedUrl_AllowedHost_ReturnsTrue(string url)
    {
        Assert.True(NavigationGuard.IsAllowedUrl(url));
    }

    [Theory]
    [InlineData("https://evil.com/")]
    [InlineData("https://not-icloud.com/")]
    [InlineData("https://icloud.com.evil.com/")]
    [InlineData("https://fakeicloud.com/")]
    public void IsAllowedUrl_BlockedHost_ReturnsFalse(string url)
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
