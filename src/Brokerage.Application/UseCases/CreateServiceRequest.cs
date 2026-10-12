using Brokerage.Application.Contracts;
using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.UseCases;

public class CreateServiceRequest
{
    private readonly WorkflowService _workflowService;
    private readonly IServiceRequestRepository _serviceRequestRepository;

    public CreateServiceRequest(
        WorkflowService workflowService,
        IServiceRequestRepository serviceRequestRepository)
    {
        _workflowService = workflowService;
        _serviceRequestRepository = serviceRequestRepository;
    }

    public async Task<ServiceRequest> ExecuteAsync(
        ServiceCode serviceCode,
        string initialStageCode,
        string applicantUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(serviceCode))
            throw new ArgumentOutOfRangeException(nameof(serviceCode), "Unsupported service code.");

        var request = new ServiceRequest(serviceCode, applicantUserId);
        var initialStage = _workflowService.CreateStage(request, initialStageCode);

        await _serviceRequestRepository.AddAsync(request, cancellationToken);
        await _serviceRequestRepository.AddWorkflowStageAsync(initialStage, cancellationToken);
        await _serviceRequestRepository.SaveChangesAsync(cancellationToken);

        return request;
    }
}