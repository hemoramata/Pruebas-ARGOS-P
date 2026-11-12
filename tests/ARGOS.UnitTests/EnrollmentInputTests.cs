using ARGOS.API;

namespace ARGOS.UnitTests;

public sealed class EnrollmentInputTests
{
    [Theory]
    [InlineData("2025-10-24")]
    [InlineData("24/10/2025")]
    public void AcceptsBothExplicitDateFormats(string value)
    {
        var result = new EnrollmentInput(new StringReader(value), new StringWriter()).ReadDate("Fecha");
        Assert.Equal(new DateTime(2025,10,24), result!.Value.Date);
    }
    [Fact]
    public void InvalidDateRetriesOnlyThatFieldAndPreservesNextInput()
    {
        var output = new StringWriter();
        var input = new EnrollmentInput(new StringReader("31/02/2025\n2025-10-24\nsiguiente\n"), output);
        Assert.Equal(new DateTime(2025,10,24), input.ReadDate("Fecha")!.Value.Date);
        Assert.Equal("siguiente", input.Read("Otro dato"));
        Assert.Contains("Fecha no valida", output.ToString());
    }
    [Fact]
    public void EndMustFollowStartAndCanRemainEmpty()
    {
        var input = new EnrollmentInput(new StringReader("2025-10-24\n2025-10-23\n\n"), new StringWriter());
        var start = new DateTimeOffset(new DateTime(2025,10,24,0,0,0,DateTimeKind.Local));
        Assert.Null(input.ReadDate("Fin", optional:true, after:start));
    }
    [Fact]
    public void MissingRequiredValueRetriesAndClosedInputCancels()
    {
        var input = new EnrollmentInput(new StringReader("\nNombre\n"), new StringWriter());
        Assert.Equal("Nombre",input.Read("Responsable"));
        Assert.Throws<OperationCanceledException>(()=>input.ReadDate("Inicio"));
    }
}
