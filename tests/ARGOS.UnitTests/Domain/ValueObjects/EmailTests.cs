using ARGOS.Domain.ValueObjects;

namespace ARGOS.UnitTests.Domain.ValueObjects;

public sealed class EmailTests
{
    [Fact]
    public void Create_NormalizesAValidAddress()
    {
        var result = Email.Create("  PERSONA@EJEMPLO.COM  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("persona@ejemplo.com", result.Value.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("correo-invalido")]
    public void Create_RejectsInvalidAddresses(string? value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
    }
}
