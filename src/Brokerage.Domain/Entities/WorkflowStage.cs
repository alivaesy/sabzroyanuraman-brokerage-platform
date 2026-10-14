namespace Brokerage.Domain.Entities;

public class WorkflowStage
{
    public Guid Id { get; private set; }

    public Guid ServiceRequestId { get; private set; }

    public string StageCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    private WorkflowStage()
    {
        StageCode = string.Empty;
    }

    public WorkflowStage(
        Guid serviceRequestId,
        string stageCode)
    {
        if (serviceRequestId == Guid.Empty)
            throw new ArgumentException("Service request ID cannot be empty.", nameof(serviceRequestId));

        if (string.IsNullOrWhiteSpace(stageCode))
            throw new ArgumentException("Stage code cannot be empty.", nameof(stageCode));

        Id = Guid.NewGuid();
        ServiceRequestId = serviceRequestId;
        StageCode = stageCode;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete()
    {
        CompletedAt = DateTimeOffset.UtcNow;
    }
}
