using Brokerage.Domain.Enums;

namespace Brokerage.Domain.Entities;

public class ServiceRequest
{
    public Guid Id { get; private set; }
    public string ApplicantUserId { get; private set; }
    public ServiceCode ServiceCode { get; private set; }
    public RequestStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? CurrentWorkflowStageId { get; private set; }
    public string? OrganizationTrackingId { get; private set; }
    public string? OrganizationStatus { get; private set; }

    private ServiceRequest() { }

    public ServiceRequest(ServiceCode serviceCode, string applicantUserId)
    {
        if (string.IsNullOrWhiteSpace(applicantUserId))
            throw new ArgumentException("Applicant user ID cannot be empty.", nameof(applicantUserId));

        Id = Guid.NewGuid();
        ApplicantUserId = applicantUserId;
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

    public void SetOrganizationTrackingId(string trackingId)
    {
        if (string.IsNullOrWhiteSpace(trackingId))
            throw new ArgumentException("Organization tracking ID cannot be empty.", nameof(trackingId));

        OrganizationTrackingId = trackingId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetOrganizationStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Organization status cannot be empty.", nameof(status));

        OrganizationStatus = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetCurrentWorkflowStage(Guid workflowStageId)
    {
        CurrentWorkflowStageId = workflowStageId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
