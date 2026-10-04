using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Brokerage.Application.Authorization;

namespace Brokerage.Api.Authentication;

public sealed class DevelopmentAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Development";

    public DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User-Id", out var userId) ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers["X-Test-User-Role"].FirstOrDefault()
            ?? UserRole.Applicant.ToString();

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid test role."));
        }

        var claims = new List<Claim>
        {
            new(IdentityClaims.UserId, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(IdentityClaims.Role, parsedRole.ToString()),
            new(ClaimTypes.Role, parsedRole.ToString())
        };

        if (string.Equals(
                Request.Headers["X-Test-Mfa-Verified"].FirstOrDefault(),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(IdentityClaims.MfaVerified, "true"));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
