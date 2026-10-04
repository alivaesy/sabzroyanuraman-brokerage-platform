using Brokerage.Application.Integration;
using Brokerage.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Brokerage.Application.UseCases;

public sealed record VerifyIdentityResult(string UserId, bool Verified);

public sealed class VerifyIdentity
{
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly ILogger<VerifyIdentity> _logger;

    public VerifyIdentity(
        IIdentityVerificationService identityVerificationService,
        ILogger<VerifyIdentity> logger)
    {
        _identityVerificationService = identityVerificationService;
        _logger = logger;
    }

    public async Task<VerifyIdentityResult> ExecuteAsync(
        string authenticatedUserId,
        string nationalIdentifier,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
            throw new ArgumentException("Authenticated user is required.", nameof(authenticatedUserId));

        var verified = await _identityVerificationService.VerifyAsync(
            nationalIdentifier,
            cancellationToken);

        _logger.LogInformation(
            "AuditEvent {@AuditEvent}",
            new AuditEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow, "IdentityVerification",
                correlationId ?? string.Empty, null, "IdentityVerification",
                verified ? "Success" : "Failure", null,
                verified ? "Verified" : "Rejected"));

        return new VerifyIdentityResult(authenticatedUserId, verified);
    }
}
