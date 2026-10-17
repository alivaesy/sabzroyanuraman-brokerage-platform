namespace Brokerage.Application.Validation;

public sealed class IranianNationalIdentifierValidator : INationalIdentifierValidator
{
    public bool IsValid(string? nationalIdentifier)
    {
        if (string.IsNullOrWhiteSpace(nationalIdentifier))
            return false;

        // Accept ASCII, Persian, and Arabic-Indic digits, but normalize before
        // arithmetic: char.IsDigit alone accepts Unicode digits that cannot be
        // safely converted by subtracting ASCII '0'.
        var value = new string(nationalIdentifier.Trim().Select(NormalizeDigit).ToArray());
        if (value.Length != 10 || value.Any(character => character is < '0' or > '9'))
            return false;

        // Repeated-digit placeholders (for example 0000000000) are not valid
        // national identifiers even when their checksum happens to match.
        if (value.All(character => character == value[0]))
            return false;

        var checkDigit = value[9] - '0';
        var sum = 0;

        for (var index = 0; index < 9; index++)
            sum += (value[index] - '0') * (10 - index);

        var remainder = sum % 11;
        var expected = remainder < 2 ? remainder : 11 - remainder;

        return checkDigit == expected;
    }

    private static char NormalizeDigit(char character) => character switch
    {
        >= '\u06F0' and <= '\u06F9' => (char)('0' + character - '\u06F0'),
        >= '\u0660' and <= '\u0669' => (char)('0' + character - '\u0660'),
        _ => character
    };
}
