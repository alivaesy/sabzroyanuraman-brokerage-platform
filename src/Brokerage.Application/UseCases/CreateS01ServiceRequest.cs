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

    public CreateS01ServiceRequest(
        WorkflowService workflowService,
        IIdentityVerificationService identityVerificationService,
        IOrganizationIntegrationService organizationIntegrationService,
        IServiceRequestRepository? serviceRequestRepository = null)
    {
        _workflowService = workflowService;
        _identityVerificationService = identityVerificationService;
        _organizationIntegrationService = organizationIntegrationService;
        _serviceRequestRepository = serviceRequestRepository;
    }

    public async Task<ServiceRequest> ExecuteAsync(
        CreateS01RequestModel model,
        CancellationToken cancellationToken = default)
    {
        var isVerified = await _identityVerificationService.VerifyAsync(
            model.NationalIdentifier,
            cancellationToken);

        if (!isVerified)
        {
            throw new BrokerageException(
                "Identity verification failed.");
        }

        var trackingId = await _organizationIntegrationService.SubmitAsync(
            ServiceCode.S01.ToString(),
            cancellationToken);

        var request = new ServiceRequest(ServiceCode.S01);
        request.SetOrganizationTrackingId(trackingId);
        request.ChangeStatus(RequestStatus.WaitingForOrganization);

        var workflowStage = _workflowService.CreateStage(
            request,
            S01StageCode.IdentityVerification.ToString());

        if (_serviceRequestRepository is not null)
        {
            await _serviceRequestRepository.AddAsync(request, cancellationToken);
            await _serviceRequestRepository.AddWorkflowStageAsync(workflowStage, cancellationToken);
            await _serviceRequestRepository.SaveChangesAsync(cancellationToken);
        }

        return request;
    }
}
