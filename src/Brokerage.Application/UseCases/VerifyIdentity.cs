using Brokerage.Application.Contracts;
using Brokerage.Application.Integration;
using Brokerage.Application.Models;
using Brokerage.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Brokerage.Application.UseCases;

public sealed record VerifyIdentityResult(string UserId, bool Verified);

public sealed class VerifyIdentity
{
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly IIdentityVerificationStateRepository _stateRepository;
    private readonly ILogger<VerifyIdentity> _logger;
    private readonly IAuditEventWriter? _auditEventWriter;

    public VerifyIdentity(IIdentityVerificationService identityVerificationService,
        IIdentityVerificationStateRepository stateRepository, ILogger<VerifyIdentity> logger, IAuditEventWriter? auditEventWriter = null)
    {
        _identityVerificationService = identityVerificationService;
        _stateRepository = stateRepository;
        _logger = logger;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<VerifyIdentityResult> ExecuteAsync(string authenticatedUserId,
        string nationalIdentifier, string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
            throw new ArgumentException("Authenticated user is required.", nameof(authenticatedUserId));

        var verified = await _identityVerificationService.VerifyAsync(nationalIdentifier, cancellationToken);
        var occurredAt = DateTimeOffset.UtcNow;
        await _stateRepository.SaveResultAsync(authenticatedUserId, verified, occurredAt, cancellationToken);

        var auditEvent = new AuditEvent(Guid.NewGuid(), occurredAt,
            "IdentityVerification", correlationId ?? string.Empty, null, "IdentityVerification",
            verified ? "Success" : "Failure", null, verified ? "Verified" : "Rejected",
            authenticatedUserId);

        if (_auditEventWriter is not null)
            await _auditEventWriter.WriteAsync(auditEvent, cancellationToken);
        else
            _logger.LogInformation("AuditEvent {@AuditEvent}", auditEvent);

        return new VerifyIdentityResult(authenticatedUserId, verified);
    }
}
