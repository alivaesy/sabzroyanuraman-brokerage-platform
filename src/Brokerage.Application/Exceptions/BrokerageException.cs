namespace Brokerage.Application.Exceptions;

public class BrokerageException : Exception
{
    public BrokerageException(string message)
        : base(message)
    {
    }
}
