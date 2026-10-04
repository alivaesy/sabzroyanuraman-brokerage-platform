namespace Brokerage.Application.Validation;

public interface INationalIdentifierValidator
{
    bool IsValid(string? nationalIdentifier);
}
