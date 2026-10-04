using Brokerage.Application.Authentication;

namespace Brokerage.Application.Tests;

public class OtpServiceTests
{
    [Fact]
    public async Task IssueAsync_ReturnsChallengeWithoutPersistingOtpCode()
    {
        var service = new InMemoryOtpService();

        var challenge = await service.IssueAsync("TEST-USER-001");

        Assert.False(string.IsNullOrWhiteSpace(challenge.ChallengeId));
        Assert.True(challenge.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task VerifyAsync_UnknownChallenge_ReturnsFalse()
    {
        var service = new InMemoryOtpService();

        var result = await service.VerifyAsync("missing", "123456");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_ChallengeCanOnlyBeConsumedOnce()
    {
        var service = new InMemoryOtpService();

        var challenge = await service.IssueAsync("TEST-USER-001");

        Assert.False(await service.VerifyAsync(challenge.ChallengeId, "000000"));
        Assert.False(await service.VerifyAsync(challenge.ChallengeId, "000000"));
    }

    [Fact]
    public async Task VerifyAsync_LocksChallengeAfterFiveFailedAttempts()
    {
        var service = new InMemoryOtpService();

        var challenge = await service.IssueAsync("TEST-USER-001");

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.False(await service.VerifyAsync(challenge.ChallengeId, "000000"));

        Assert.False(await service.VerifyAsync(challenge.ChallengeId, "000000"));
    }
}
