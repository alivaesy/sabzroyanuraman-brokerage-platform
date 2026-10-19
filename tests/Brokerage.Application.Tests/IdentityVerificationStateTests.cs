using Brokerage.Domain.Entities;

namespace Brokerage.Application.Tests;

public class IdentityVerificationStateTests
{
    [Fact]
    public void Constructor_WithUserIdAtPersistenceLimit_Succeeds()
    {
        var userId = new string('u', 200);

        var state = new IdentityVerificationState(userId);

        Assert.Equal(userId, state.UserId);
    }

    [Fact]
    public void Constructor_WithUserIdLongerThanPersistenceLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new IdentityVerificationState(new string('u', 201)));
    }

    [Fact]
    public void SetResult_WhenVerificationFails_ClearsPreviousVerifiedTimestamp()
    {
        var state = new IdentityVerificationState("user-1");
        var verifiedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        state.SetResult(true, verifiedAt);

        var rejectedAt = DateTimeOffset.UtcNow;
        state.SetResult(false, rejectedAt);

        Assert.False(state.IsVerified);
        Assert.Null(state.VerifiedAt);
        Assert.Equal(rejectedAt, state.UpdatedAt);
    }
}
