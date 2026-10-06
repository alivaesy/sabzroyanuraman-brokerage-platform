using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class ServiceRequestTests
{
    [Fact]
    public void Constructor_CreatesRequestWithInitialValues()
    {
        var before = DateTimeOffset.UtcNow;

        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");

        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("test-applicant", request.ApplicantUserId);
        Assert.Equal(ServiceCode.S01, request.ServiceCode);
        Assert.Equal(RequestStatus.Created, request.Status);
        Assert.True(request.CreatedAt >= before);
        Assert.True(request.CreatedAt <= after);
        Assert.Equal(request.CreatedAt, request.UpdatedAt);
        Assert.Null(request.CurrentWorkflowStageId);
        Assert.Null(request.OrganizationTrackingId);
        Assert.Null(request.OrganizationStatus);
    }

    [Fact]
    public void Constructor_WithEmptyApplicantUserId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ServiceRequest(ServiceCode.S01, " "));
    }

    [Fact]
    public void SetOrganizationStatus_StoresStatusAndUpdatesTimestamp()
    {
        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");
        var before = request.UpdatedAt;

        request.SetOrganizationStatus("Approved");

        Assert.Equal("Approved", request.OrganizationStatus);
        Assert.True(request.UpdatedAt >= before);
    }

    [Fact]
    public void SetOrganizationStatus_WithEmptyValue_Throws()
    {
        var request = new ServiceRequest(ServiceCode.S01, "test-applicant");

        Assert.Throws<ArgumentException>(
            () => request.SetOrganizationStatus(" "));
    }
}
