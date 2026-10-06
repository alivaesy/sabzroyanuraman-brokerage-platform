namespace Brokerage.Application.Validation;

public sealed class IranianNationalIdentifierValidator : INationalIdentifierValidator
{
    public bool IsValid(string? nationalIdentifier)
    {
        if (string.IsNullOrWhiteSpace(nationalIdentifier))
            return false;

        var value = nationalIdentifier.Trim();
        if (value.Length != 10 || !value.All(char.IsDigit))
            return false;

        var checkDigit = value[9] - '0';
        var sum = 0;

        for (var index = 0; index < 9; index++)
            sum += (value[index] - '0') * (10 - index);

        var remainder = sum % 11;
        var expected = remainder < 2 ? remainder : 11 - remainder;

        return checkDigit == expected;
    }
}
