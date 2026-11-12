using ARGOS.API;

namespace ARGOS.UnitTests;

public sealed class EnrollmentDiagnosticsTests
{
    [Theory]
    [InlineData(EnrollmentStage.Preparation, "PostgreSQL")]
    [InlineData(EnrollmentStage.Data, "vigencia")]
    [InlineData(EnrollmentStage.SendingEmail, "-UpdateMailCredential")]
    [InlineData(EnrollmentStage.VerifyingEmail, "codigo nuevo")]
    [InlineData(EnrollmentStage.Saving, "Compruebe las cuentas")]
    public void GivesSpecificSafeGuidance(EnrollmentStage stage, string expected)
        => Assert.Contains(expected, EnrollmentDiagnostics.Describe(stage));
}
