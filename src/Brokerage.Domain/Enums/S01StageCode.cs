namespace Brokerage.Domain.Enums;

public enum S01StageCode
{
    Initial = 1,
    IdentityVerification = 2,
    InitialReview = 3,
    ExpertReview = 4,
    RequiredFormalities = 5,
    OrganizationSubmission = 6,
    OrganizationFollowUp = 7,
    ResultNotification = 8
}