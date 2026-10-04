using System.Security.Claims;
using Brokerage.Application.Authorization;

namespace Brokerage.Api.Authorization;

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _principal = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
    }

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;

    public string? UserId => _principal.FindFirst(IdentityClaims.UserId)?.Value;

    public string? Role => _principal.FindFirst(IdentityClaims.Role)?.Value;

    public bool IsMfaVerified => _principal.FindFirst(IdentityClaims.MfaVerified)?.Value == "true";
}
