using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;

namespace ARGOS.UnitTests.Domain.ValueObjects;

public sealed class CaseFileNumberTests
{
    [Theory]
    [InlineData("US-DI-0001-2026")]
    [InlineData("AB-12-9999-2030")]
    public void Create_AcceptsTheOfficialFormat(string value)
    {
        var result = CaseFileNumber.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.Value);
    }

    [Fact]
    public void Create_NormalizesLettersAndWhitespace()
    {
        var result = CaseFileNumber.Create("  us-di-0001-2026  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("US-DI-0001-2026", result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("US-DI-001-2026")]
    [InlineData("US-DI-0000-2026")]
    [InlineData("US-DI-0001-26")]
    [InlineData("USUARIO-DI-0001-2026")]
    public void Create_RejectsInvalidNumbers(string value)
    {
        var result = CaseFileNumber.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal("CaseFile.InvalidNumberFormat", result.Error.Code);
    }
}
