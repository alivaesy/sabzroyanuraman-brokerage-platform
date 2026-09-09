using Brokerage.Domain.Enums;

namespace Brokerage.Domain.Entities;

public class ServiceRequest
{
    public Guid Id { get; private set; }

    public ServiceCode ServiceCode { get; private set; }

    public RequestStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public Guid? CurrentWorkflowStageId { get; private set; }

    private ServiceRequest()
    {
    }

    public ServiceRequest(ServiceCode serviceCode)
    {
        Id = Guid.NewGuid();
        ServiceCode = serviceCode;
        Status = RequestStatus.Created;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void ChangeStatus(RequestStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
    public void SetCurrentWorkflowStage(Guid workflowStageId)
    {
        CurrentWorkflowStageId = workflowStageId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
