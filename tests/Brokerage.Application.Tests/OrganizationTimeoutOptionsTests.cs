using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationTimeoutOptionsTests
{
    [Fact]
    public void DefaultTimeout_Is30Seconds()
    {
        var options = new OrganizationTimeoutOptions();

        Assert.Equal(
            TimeSpan.FromSeconds(30),
            options.Timeout);
    }

    [Fact]
    public void CustomTimeout_CanBeConfigured()
    {
        var options = new OrganizationTimeoutOptions
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        Assert.Equal(
            TimeSpan.FromSeconds(60),
            options.Timeout);
    }
}