namespace Brokerage.Application.Authorization;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Role { get; }

    bool IsMfaVerified { get; }
}
