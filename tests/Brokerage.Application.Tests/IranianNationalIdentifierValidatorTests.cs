using Brokerage.Application.Validation;

namespace Brokerage.Application.Tests;

public class IranianNationalIdentifierValidatorTests
{
    private readonly IranianNationalIdentifierValidator _validator = new();

    [Theory]
    [InlineData("1234567891")]
    [InlineData("1231231238")]
    public void IsValid_WithValidNationalIdentifier_ReturnsTrue(string nationalIdentifier)
    {
        Assert.True(_validator.IsValid(nationalIdentifier));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("12345678901")]
    [InlineData("1234567890")]
    [InlineData("TEST-123")]
    public void IsValid_WithInvalidNationalIdentifier_ReturnsFalse(string? nationalIdentifier)
    {
        Assert.False(_validator.IsValid(nationalIdentifier));
    }
}
