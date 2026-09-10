using Brokerage.Application.Integration;

namespace Brokerage.Application.Tests;

public class OrganizationRetryOptionsTests
{
    [Fact]
    public void DefaultValues_DoNotEnableRetry()
    {
        var options = new OrganizationRetryOptions();

        Assert.Equal(0, options.MaxRetryCount);
        Assert.Equal(TimeSpan.Zero, options.InitialBackoff);
    }

    [Fact]
    public void Values_CanBeConfigured()
    {
        var options = new OrganizationRetryOptions
        {
            MaxRetryCount = 3,
            InitialBackoff = TimeSpan.FromSeconds(2)
        };

        Assert.Equal(3, options.MaxRetryCount);
        Assert.Equal(
            TimeSpan.FromSeconds(2),
            options.InitialBackoff);
    }
}