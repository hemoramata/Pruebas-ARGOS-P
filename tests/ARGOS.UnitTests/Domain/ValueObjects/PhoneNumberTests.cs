using ARGOS.Domain.ValueObjects;

namespace ARGOS.UnitTests.Domain.ValueObjects;

public sealed class PhoneNumberTests
{
    [Fact]
    public void Create_NormalizesCommonFormatting()
    {
        var result = PhoneNumber.Create("+506 8888-7777");

        Assert.True(result.IsSuccess);
        Assert.Equal("+50688887777", result.Value.Value);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("8888-ABCD")]
    public void Create_RejectsInvalidNumbers(string value)
    {
        var result = PhoneNumber.Create(value);

        Assert.True(result.IsFailure);
    }
}
