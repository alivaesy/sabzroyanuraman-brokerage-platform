using Brokerage.Application.Contracts;
using Brokerage.Application.Models;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.UseCases;

public class CreateServiceRequest
{
    private readonly WorkflowService _workflowService;
    private readonly IServiceRequestRepository _serviceRequestRepository;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IUnitOfWork _unitOfWork;

    public CreateServiceRequest(
        WorkflowService workflowService,
        IServiceRequestRepository serviceRequestRepository,
        IAuditEventWriter auditEventWriter,
        IUnitOfWork unitOfWork)
    {
        _workflowService = workflowService;
        _serviceRequestRepository = serviceRequestRepository;
        _auditEventWriter = auditEventWriter;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceRequest> ExecuteAsync(
        ServiceCode serviceCode,
        string initialStageCode,
        string applicantUserId,
        CancellationToken cancellationToken = default,
        string? correlationId = null,
        string? actorRole = null,
        string? ipAddress = null)
    {
        if (!Enum.IsDefined(serviceCode))
            throw new ArgumentOutOfRangeException(nameof(serviceCode), "Unsupported service code.");

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var request = new ServiceRequest(serviceCode, applicantUserId);
        var initialStage = _workflowService.CreateStage(request, initialStageCode);

        await _serviceRequestRepository.AddAsync(request, cancellationToken);
        await _serviceRequestRepository.AddWorkflowStageAsync(initialStage, cancellationToken);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        var effectiveCorrelationId = correlationId ?? string.Empty;
        var occurredAt = DateTimeOffset.UtcNow;

        await _auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                occurredAt,
                "WorkflowStageCreated",
                effectiveCorrelationId,
                request.Id,
                initialStage.StageCode,
                "Success",
                null,
                initialStage.StageCode,
                applicantUserId,
                actorRole,
                ipAddress), cancellationToken);

        await _auditEventWriter.WriteAsync(new AuditEvent(
                Guid.NewGuid(),
                occurredAt,
                "ServiceRequestCreated",
                effectiveCorrelationId,
                request.Id,
                initialStage.StageCode,
                "Success",
                null,
                request.Status.ToString(),
                applicantUserId,
                actorRole,
                ipAddress), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return request;
    }
}
