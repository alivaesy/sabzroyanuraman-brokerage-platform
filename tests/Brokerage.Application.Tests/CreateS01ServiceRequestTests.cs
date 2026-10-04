using Brokerage.Application.Integration;
using Brokerage.Application.Exceptions;
using Brokerage.Application.Services;
using Brokerage.Application.UseCases;
using Brokerage.Domain.Enums;
using Brokerage.Infrastructure.Integration;
using Brokerage.Application.Models;

namespace Brokerage.Application.Tests;

public class CreateS01ServiceRequestTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidTestIdentity_CreatesS01Request()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationApiClient = new MockOrganizationApiClient();

        var organizationIntegrationService =
            new MockOrganizationIntegrationService(organizationApiClient);

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "TEST-123" });

        Assert.Equal(ServiceCode.S01, request.ServiceCode);
        Assert.Equal(RequestStatus.WaitingForOrganization, request.Status);
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.NotEqual(Guid.Empty, request.CurrentWorkflowStageId);
        Assert.False(string.IsNullOrWhiteSpace(request.OrganizationTrackingId));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidIdentity_PersistsOrganizationTrackingIdAndWaitingStatus()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new TestOrganizationIntegrationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        var request = await useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "TEST-123" });

        Assert.Equal("TEST-TRACKING-ID", request.OrganizationTrackingId);
        Assert.Equal(RequestStatus.WaitingForOrganization, request.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidTestIdentity_ThrowsBrokerageException()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationApiClient = new MockOrganizationApiClient();

        var organizationIntegrationService =
            new MockOrganizationIntegrationService(organizationApiClient);

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        await Assert.ThrowsAsync<BrokerageException>(
            () => useCase.ExecuteAsync(
                new CreateS01RequestModel { NationalIdentifier = "TEST-999" }));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidIdentity_SubmitsToOrganizationIntegration()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new TestOrganizationIntegrationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        await useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "TEST-123" });

        Assert.Equal("S01", organizationIntegrationService.SubmittedServiceCode);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenToOrganizationIntegration()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new TestOrganizationIntegrationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        using var cts = new CancellationTokenSource();

        await useCase.ExecuteAsync(
            new CreateS01RequestModel { NationalIdentifier = "TEST-123" },
            cts.Token);

        Assert.Equal(cts.Token, organizationIntegrationService.ReceivedCancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidIdentity_DoesNotSubmitToOrganizationIntegration()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new TestOrganizationIntegrationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        await Assert.ThrowsAsync<BrokerageException>(
            () => useCase.ExecuteAsync(
                new CreateS01RequestModel { NationalIdentifier = "TEST-999" }));

        Assert.Null(organizationIntegrationService.SubmittedServiceCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrganizationSubmissionFails_ThrowsBrokerageException()
    {
        var workflowService = new WorkflowService();
        var identityService = new MockIdentityVerificationService();
        var organizationIntegrationService = new FailingOrganizationIntegrationService();

        var useCase = new CreateS01ServiceRequest(
            workflowService,
            identityService,
            organizationIntegrationService);

        await Assert.ThrowsAsync<BrokerageException>(
            () => useCase.ExecuteAsync(
                new CreateS01RequestModel { NationalIdentifier = "TEST-123" }));
    }
}

internal sealed class TestOrganizationIntegrationService : IOrganizationIntegrationService
{
    public string? SubmittedServiceCode { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        SubmittedServiceCode = serviceCode;
        ReceivedCancellationToken = cancellationToken;
        return Task.FromResult("TEST-TRACKING-ID");
    }

    public Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("TEST-STATUS");
    }
}

internal sealed class FailingOrganizationIntegrationService : IOrganizationIntegrationService
{
    public Task<string> SubmitAsync(
        string serviceCode,
        CancellationToken cancellationToken = default)
    {
        throw new BrokerageException("Organization API submission failed.");
    }

    public Task<string> GetStatusAsync(
        string trackingId,
        CancellationToken cancellationToken = default)
    {
        throw new BrokerageException("Organization API status request failed.");
    }
}
