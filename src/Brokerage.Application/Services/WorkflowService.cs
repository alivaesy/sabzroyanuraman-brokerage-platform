using Brokerage.Domain.Entities;

namespace Brokerage.Application.Services;

public class WorkflowService
{
    public WorkflowStage CreateStage(
        ServiceRequest serviceRequest,
        string stageCode)
    {
        var stage = new WorkflowStage(
            serviceRequest.Id,
            stageCode);

        serviceRequest.SetCurrentWorkflowStage(stage.Id);

        return stage;
    }
}