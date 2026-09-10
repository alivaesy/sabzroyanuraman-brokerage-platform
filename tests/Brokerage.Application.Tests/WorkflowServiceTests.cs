using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class WorkflowServiceTests
{
    [Fact]
    public void CreateStage_AssignsStageToServiceRequest()
    {
        var workflowService = new WorkflowService();
        var request = new ServiceRequest(ServiceCode.S01);

        var stage = workflowService.CreateStage(
            request,
            "IdentityVerification");

        Assert.NotEqual(Guid.Empty, stage.Id);
        Assert.Equal(
            stage.Id,
            request.CurrentWorkflowStageId);
        Assert.Equal(
            request.Id,
            stage.ServiceRequestId);
        Assert.Equal(
            "IdentityVerification",
            stage.StageCode);
    }
}