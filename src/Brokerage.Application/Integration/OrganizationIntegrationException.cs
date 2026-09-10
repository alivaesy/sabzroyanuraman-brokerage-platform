namespace Brokerage.Application.Integration;

public class OrganizationIntegrationException : Exception
{
    public OrganizationIntegrationException(string message)
        : base(message)
    {
    }

    public OrganizationIntegrationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}