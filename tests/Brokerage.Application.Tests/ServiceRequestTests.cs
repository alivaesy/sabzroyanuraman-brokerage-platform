using Brokerage.Domain.Entities;
using Brokerage.Domain.Enums;

namespace Brokerage.Application.Tests;

public class ServiceRequestTests
{
    [Fact]
    public void Constructor_CreatesRequestWithInitialValues()
    {
        var before = DateTimeOffset.UtcNow;

        var request = new ServiceRequest(ServiceCode.S01);

        var after = DateTimeOffset.UtcNow;

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal(ServiceCode.S01, request.ServiceCode);
        Assert.Equal(RequestStatus.Created, request.Status);
        Assert.True(request.CreatedAt >= before);
        Assert.True(request.CreatedAt <= after);
        Assert.Equal(request.CreatedAt, request.UpdatedAt);
        Assert.Null(request.CurrentWorkflowStageId);
    }
}