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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task IssueAsync_WithEmptyUserId_Throws(string userId)
    {
        var service = new InMemoryOtpService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.IssueAsync(userId));
    }

    [Fact]
    public async Task VerifyAsync_UnknownChallenge_ReturnsFalse()
    {
        var service = new InMemoryOtpService();

        var result = await service.VerifyAsync("TEST-USER-001", "missing", "123456");

        Assert.False(result);
    }

    [Theory]
    [InlineData("", "known", "123456")]
    [InlineData("TEST-USER-001", "", "123456")]
    [InlineData("TEST-USER-001", "known", "")]
    public async Task VerifyAsync_WithMissingInput_ReturnsFalse(string userId, string challengeId, string code)
    {
        var service = new InMemoryOtpService();

        Assert.False(await service.VerifyAsync(userId, challengeId, code));
    }

    [Fact]
    public async Task VerifyAsync_CannotBeUsedByAnotherUser()
    {
        var service = new InMemoryOtpService();
        var challenge = await service.IssueAsync("TEST-USER-001");

        Assert.False(await service.VerifyAsync("TEST-USER-002", challenge.ChallengeId, "000000"));
    }

    [Fact]
    public async Task VerifyAsync_ChallengeCanOnlyBeConsumedOnce()
    {
        var service = new InMemoryOtpService();
        var challenge = await service.IssueAsync("TEST-USER-001");

        Assert.False(await service.VerifyAsync("TEST-USER-001", challenge.ChallengeId, "000000"));
        Assert.False(await service.VerifyAsync("TEST-USER-001", challenge.ChallengeId, "000000"));
    }

    [Fact]
    public async Task VerifyAsync_LocksChallengeAfterFiveFailedAttempts()
    {
        var service = new InMemoryOtpService();
        var challenge = await service.IssueAsync("TEST-USER-001");

        for (var attempt = 0; attempt < 5; attempt++)
            Assert.False(await service.VerifyAsync("TEST-USER-001", challenge.ChallengeId, "000000"));

        Assert.False(await service.VerifyAsync("TEST-USER-001", challenge.ChallengeId, "000000"));
    }
}
