using Brokerage.Application.Exceptions;
using Brokerage.Application.Services;
using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Infrastructure.Integration;

namespace Brokerage.Application.Tests;

public class CreateS01ServiceRequestTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidTestIdentity_CreatesS01Request()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService);

        var request = await useCase.ExecuteAsync("TEST-123");

        Assert.Equal(ServiceCode.S01, request.ServiceCode);
        Assert.Equal(RequestStatus.Created, request.Status);
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.NotEqual(Guid.Empty, request.CurrentWorkflowStageId);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTestIdentity_ThrowsBrokerageException()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService);

        await Assert.ThrowsAsync<BrokerageException>(
            () => useCase.ExecuteAsync("TEST-999"));
    }
}