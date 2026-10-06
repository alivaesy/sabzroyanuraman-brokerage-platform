using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Brokerage.Application.Services;

namespace Brokerage.Application.UseCases;

public class CreateServiceRequest
{
    private readonly WorkflowService _workflowService;

    public CreateServiceRequest(WorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public ServiceRequest Execute(
        ServiceCode serviceCode,
        string initialStageCode,
        string applicantUserId)
    {
        var request = new ServiceRequest(serviceCode, applicantUserId);

        _workflowService.CreateStage(
            request,
            initialStageCode);

        return request;
    }
}
