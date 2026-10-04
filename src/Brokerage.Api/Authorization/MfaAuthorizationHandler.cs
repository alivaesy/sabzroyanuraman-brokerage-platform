using Brokerage.Application.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Brokerage.Api.Authorization;

public sealed class MfaAuthorizationHandler : AuthorizationHandler<MfaRequirement>
{
    private readonly IMfaVerificationStore _verificationStore;

    public MfaAuthorizationHandler(IMfaVerificationStore verificationStore)
    {
        _verificationStore = verificationStore;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaRequirement requirement)
    {
        var userId = context.User.FindFirst(IdentityClaims.UserId)?.Value;

        if (!string.IsNullOrWhiteSpace(userId) &&
            _verificationStore.IsVerified(userId, DateTimeOffset.UtcNow))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
