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
        string initialStageCode)
    {
        var request = new ServiceRequest(serviceCode);

        _workflowService.CreateStage(
            request,
            initialStageCode);

        return request;
    }
}