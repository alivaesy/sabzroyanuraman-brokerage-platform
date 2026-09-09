namespace Brokerage.Domain.Enums;

public enum RequestStatus
{
    Created = 1,
    InReview = 2,
    InProgress = 3,
    WaitingForOrganization = 4,
    Completed = 5,
    Rejected = 6,
    Failed = 7
}
