using Microsoft.Extensions.Logging;
using Brokerage.Application.Contracts;
using Brokerage.Application.Integration;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Brokerage.Application.Exceptions;
using Brokerage.Application.Models;

namespace Brokerage.Application.UseCases;

public class CreateS01ServiceRequest
{
    private readonly WorkflowService _workflowService;
    private readonly IIdentityVerificationService _identityVerificationService;
    private readonly IOrganizationIntegrationService _organizationIntegrationService;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly ILogger<CreateS01ServiceRequest>? _logger;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IUnitOfWork _unitOfWork;

    public CreateS01ServiceRequest(
        WorkflowService workflowService,
        IIdentityVerificationService identityVerificationService,
        IOrganizationIntegrationService organizationIntegrationService,
        IServiceRequestRepository serviceRequestRepository,
        IAuditEventWriter auditEventWriter,
        IUnitOfWork unitOfWork,
        ILogger<CreateS01ServiceRequest>? logger = null)
    {
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        _identityVerificationService = identityVerificationService ?? throw new ArgumentNullException(nameof(identityVerificationService));
        _organizationIntegrationService = organizationIntegrationService ?? throw new ArgumentNullException(nameof(organizationIntegrationService));
        _serviceRequestRepository = serviceRequestRepository ?? throw new ArgumentNullException(nameof(serviceRequestRepository));
        _logger = logger;
        _auditEventWriter = auditEventWriter ?? throw new ArgumentNullException(nameof(auditEventWriter));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ServiceRequest> ExecuteAsync(
        CreateS01RequestModel model,
        string applicantUserId,
        string? correlationId = null,
        CancellationToken cancellationToken = default,
        string? actorRole = null,
        string? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (string.IsNullOrWhiteSpace(applicantUserId))
            throw new ArgumentException("Applicant user ID cannot be empty.", nameof(applicantUserId));

        var isVerified = await _identityVerificationService.VerifyAsync(model.NationalIdentifier, cancellationToken);
        if (!isVerified)
            throw new BrokerageException("Identity verification failed.");

        // Keep the external call outside the local database transaction. The external
        // submission cannot be rolled back by a local database rollback.
        string trackingId;
        try
        {
            trackingId = await _organizationIntegrationService.SubmitAsync(
                ServiceCode.S01.ToString(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No local request ID exists yet. Record a minimal failure event without
            // persisting provider payloads, exception text, or the applicant identifier.
            await _auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(), DateTimeOffset.UtcNow, "OrganizationSubmissionFailed",
                correlationId ?? string.Empty, null, S01StageCode.OrganizationSubmission.ToString(),
                "Failure", null, "ProviderError", applicantUserId, actorRole, ipAddress),
                CancellationToken.None);
            throw;
        }

        // Once the external API has returned a tracking ID, caller/request cancellation
        // must not prevent us from recording that known remote side effect locally.
        // This does not solve process crashes or database failures after remote success.
        var persistenceCancellationToken = CancellationToken.None;

        // All local request, workflow, and audit writes share the scoped DbContext
        // transaction in production. Disposal without CommitAsync rolls back local writes.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(persistenceCancellationToken);

        var request = new ServiceRequest(ServiceCode.S01, applicantUserId);

        var identityVerificationStage = _workflowService.CreateStage(request, S01StageCode.IdentityVerification.ToString());
        await LogAuditAsync("WorkflowStageCreated", request.Id, identityVerificationStage.StageCode, correlationId, null, identityVerificationStage.StageCode);
        identityVerificationStage.Complete();
        await LogAuditAsync("WorkflowStageCompleted", request.Id, identityVerificationStage.StageCode, correlationId, identityVerificationStage.StageCode, identityVerificationStage.StageCode);

        var organizationSubmissionStage = _workflowService.CreateStage(request, S01StageCode.OrganizationSubmission.ToString());
        await LogAuditAsync("WorkflowStageCreated", request.Id, organizationSubmissionStage.StageCode, correlationId, null, organizationSubmissionStage.StageCode);
        organizationSubmissionStage.Complete();
        await LogAuditAsync("WorkflowStageCompleted", request.Id, organizationSubmissionStage.StageCode, correlationId, organizationSubmissionStage.StageCode, organizationSubmissionStage.StageCode);

        var organizationFollowUpStage = _workflowService.CreateStage(request, S01StageCode.OrganizationFollowUp.ToString());
        await LogAuditAsync("WorkflowStageCreated", request.Id, organizationFollowUpStage.StageCode, correlationId, null, organizationFollowUpStage.StageCode);

        request.SetOrganizationTrackingId(trackingId);
        var previousRequestStatus = request.Status;
        request.ChangeStatus(RequestStatus.WaitingForOrganization);
        await LogAuditAsync("ServiceRequestStatusChanged", request.Id, organizationFollowUpStage.StageCode, correlationId, previousRequestStatus.ToString(), request.Status.ToString());

        await _serviceRequestRepository.AddAsync(request, persistenceCancellationToken);
        await _serviceRequestRepository.AddWorkflowStageAsync(identityVerificationStage, persistenceCancellationToken);
        await _serviceRequestRepository.AddWorkflowStageAsync(organizationSubmissionStage, persistenceCancellationToken);
        await _serviceRequestRepository.AddWorkflowStageAsync(organizationFollowUpStage, persistenceCancellationToken);
        await _serviceRequestRepository.SaveChangesAsync(persistenceCancellationToken);

        await LogAuditAsync(
            "ServiceRequestCreated", request.Id, request.CurrentWorkflowStageId?.ToString() ?? string.Empty,
            correlationId, null, request.Status.ToString(), applicantUserId, actorRole, ipAddress);

        await transaction.CommitAsync(persistenceCancellationToken);

        return request;
    }

    private async Task LogAuditAsync(
        string eventType, Guid serviceRequestId, string workflowStage, string? correlationId,
        string? previousState, string? newState, string? actorUserId = null,
        string? actorRole = null, string? ipAddress = null)
    {
        var auditEvent = new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, eventType,
            correlationId ?? string.Empty, serviceRequestId, workflowStage,
            "Success", previousState, newState, actorUserId, actorRole, ipAddress);

        _logger?.LogInformation("Audit event prepared. EventType={EventType} Outcome={Outcome}", eventType, auditEvent.Outcome);
        await _auditEventWriter.WriteAsync(auditEvent, CancellationToken.None);
    }
}
