using Brokerage.Application.Contracts;
using Brokerage.Application.Integration;
using Brokerage.Application.Models;

namespace Brokerage.Application.UseCases;

public sealed record VerifyIdentityResult(string UserId, bool Verified);

public sealed class VerifyIdentity
{
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly IIdentityVerificationStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditEventWriter _auditEventWriter;

    public VerifyIdentity(
        IIdentityVerificationService identityVerificationService,
        IIdentityVerificationStateRepository stateRepository,
        IUnitOfWork unitOfWork,
        IAuditEventWriter auditEventWriter)
    {
        _identityVerificationService = identityVerificationService ?? throw new ArgumentNullException(nameof(identityVerificationService));
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditEventWriter = auditEventWriter ?? throw new ArgumentNullException(nameof(auditEventWriter));
    }

    public async Task<VerifyIdentityResult> ExecuteAsync(
        string authenticatedUserId,
        string nationalIdentifier,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authenticatedUserId))
            throw new ArgumentException("Authenticated user is required.", nameof(authenticatedUserId));

        var verified = await _identityVerificationService.VerifyAsync(nationalIdentifier, cancellationToken);
        var occurredAt = DateTimeOffset.UtcNow;

        // Identity state and its mandatory audit event must succeed or roll back together.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        await _stateRepository.SaveResultAsync(authenticatedUserId, verified, occurredAt, cancellationToken);

        await _auditEventWriter.WriteAsync(new AuditEvent(
            Guid.NewGuid(),
            occurredAt,
            "IdentityVerification",
            correlationId ?? string.Empty,
            null,
            "IdentityVerification",
            verified ? "Success" : "Failure",
            null,
            verified ? "Verified" : "Rejected",
            authenticatedUserId), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new VerifyIdentityResult(authenticatedUserId, verified);
    }
}
