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
    private readonly IServiceRequestRepository? _serviceRequestRepository;
    private readonly ILogger<CreateS01ServiceRequest>? _logger;
    private readonly IAuditEventWriter? _auditEventWriter;

    public CreateS01ServiceRequest(
        WorkflowService workflowService,
        IIdentityVerificationService identityVerificationService,
        IOrganizationIntegrationService organizationIntegrationService,
        IServiceRequestRepository? serviceRequestRepository = null,
        ILogger<CreateS01ServiceRequest>? logger = null,
        IAuditEventWriter? auditEventWriter = null)
    {
        _workflowService = workflowService;
        _identityVerificationService = identityVerificationService;
        _organizationIntegrationService = organizationIntegrationService;
        _serviceRequestRepository = serviceRequestRepository;
        _logger = logger;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<ServiceRequest> ExecuteAsync(
        CreateS01RequestModel model,
        string applicantUserId,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicantUserId))
            throw new ArgumentException("Applicant user ID cannot be empty.", nameof(applicantUserId));

        var isVerified = await _identityVerificationService.VerifyAsync(model.NationalIdentifier, cancellationToken);
        if (!isVerified)
            throw new BrokerageException("Identity verification failed.");

        var trackingId = await _organizationIntegrationService.SubmitAsync(
            ServiceCode.S01.ToString(), cancellationToken);

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

        if (_serviceRequestRepository is not null)
        {
            await _serviceRequestRepository.AddAsync(request, cancellationToken);
            await _serviceRequestRepository.AddWorkflowStageAsync(identityVerificationStage, cancellationToken);
            await _serviceRequestRepository.AddWorkflowStageAsync(organizationSubmissionStage, cancellationToken);
            await _serviceRequestRepository.AddWorkflowStageAsync(organizationFollowUpStage, cancellationToken);
            await _serviceRequestRepository.SaveChangesAsync(cancellationToken);
        }

        return request;
    }

    private async Task LogAuditAsync(string eventType, Guid serviceRequestId, string workflowStage, string? correlationId, string? previousState, string? newState)
    {
        var auditEvent = new AuditEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, eventType,
            correlationId ?? string.Empty, serviceRequestId, workflowStage,
            "Success", previousState, newState);

        if (_auditEventWriter is not null)
            await _auditEventWriter.WriteAsync(auditEvent);
        else
            _logger?.LogInformation("AuditEvent {@AuditEvent}", auditEvent);
    }
}
