using Brokerage.Application.Services;
using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class WorkflowServiceTests
{
    [Fact]
    public void CreateStage_WithEmptyStageCode_Throws()
    {
        var workflowService = new WorkflowService();
        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");

        Assert.Throws<ArgumentException>(
            () => workflowService.CreateStage(request, " "));
    }

    [Fact]
    public void WorkflowStage_WithEmptyServiceRequestId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new WorkflowStage(Guid.Empty, "IdentityVerification"));
    }

    [Fact]
    public void WorkflowStage_WithStageCodeLongerThanPersistenceLimit_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorkflowStage(Guid.NewGuid(), new string('s', 101)));
    }

    [Fact]
    public void CreateStage_AssignsStageToServiceRequest()
    {
        var workflowService = new WorkflowService();
        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");

        var stage = workflowService.CreateStage(
            request,
            "IdentityVerification");

        Assert.NotEqual(Guid.Empty, stage.Id);
        Assert.Equal(stage.Id, request.CurrentWorkflowStageId);
        Assert.Equal(request.Id, stage.ServiceRequestId);
        Assert.Equal("IdentityVerification", stage.StageCode);
    }

    [Fact]
    public void CreateStage_TrimsSurroundingWhitespaceFromStageCode()
    {
        var workflowService = new WorkflowService();
        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");

        var stage = workflowService.CreateStage(request, "  IdentityVerification  ");

        Assert.Equal("IdentityVerification", stage.StageCode);
    }

    [Fact]
    public void Complete_PreservesFirstCompletionTimestamp()
    {
        var stage = new WorkflowStage(Guid.NewGuid(), "IdentityVerification");

        stage.Complete();
        var firstCompletion = stage.CompletedAt;

        stage.Complete();

        Assert.NotNull(firstCompletion);
        Assert.Equal(firstCompletion, stage.CompletedAt);
    }
}
