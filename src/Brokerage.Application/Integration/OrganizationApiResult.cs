namespace Brokerage.Application.Integration;

public sealed class OrganizationApiResult
{
    public bool IsSuccess { get; init; }

    public OrganizationIntegrationErrorType ErrorType { get; init; }

    public string? TrackingId { get; init; }

    public string? ErrorMessage { get; init; }

    public static OrganizationApiResult Success(string? trackingId = null)
    {
        return new OrganizationApiResult
        {
            IsSuccess = true,
            TrackingId = trackingId
        };
    }

    public static OrganizationApiResult Failure(
        OrganizationIntegrationErrorType errorType,
        string? errorMessage = null)
    {
        return new OrganizationApiResult
        {
            IsSuccess = false,
            ErrorType = errorType,
            ErrorMessage = errorMessage
        };
    }
}