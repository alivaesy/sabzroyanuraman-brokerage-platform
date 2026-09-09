using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;
using Brokerage.Application.Services;

namespace Brokerage.Application.UseCases;

public class CreateS01ServiceRequest
{
    private readonly WorkflowService _workflowService;

    public CreateS01ServiceRequest(
        WorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    public ServiceRequest Execute()
    {
        var request = new ServiceRequest(ServiceCode.S01);

        _workflowService.CreateStage(
            request,
            "INITIAL");

        return request;
    }
}