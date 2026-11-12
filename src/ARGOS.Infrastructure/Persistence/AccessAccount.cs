using System.Security.Cryptography;
using ARGOS.Application.Security;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>Server credential record. Provision only through an authorized enrollment workflow.</summary>
public sealed class AccessAccount
{
    private const int Iterations = 600_000;
    private AccessAccount() { }
    public Guid PersonId { get; private set; }
    public string Identification { get; private set; } = "";
    public byte[] Salt { get; private set; } = [];
    public byte[] PasswordDigest { get; private set; } = [];
    public VerificationChannel Channel { get; private set; }
    public string Destination { get; private set; } = "";
    public string VerificationReference { get; private set; } = "";
    public bool Enabled { get; private set; }
    public bool MustChangePassword { get; private set; }
    public int FailedAttempts { get; set; }
    public long LockedUntilTicks { get; set; }

    public static AccessAccount Provision(Guid personId, string identification, string password,
        VerificationChannel channel, string destination, string verificationReference)
    {
        if (personId == Guid.Empty) throw new ArgumentException("Person is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(identification);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(verificationReference);
        if (identification.Trim().Length > 100 || destination.Length > 254 || verificationReference.Length > 200
            || !Enum.IsDefined(channel)) throw new ArgumentException("Invalid account data.");
        if (password is null || password.Length is < 8 or > 1024)
            throw new ArgumentException("La clave inicial debe tener entre 8 y 1024 caracteres.");
        var salt = RandomNumberGenerator.GetBytes(16);
        return new AccessAccount
        {
            PersonId = personId, Identification = identification.Trim(), Channel = channel,
            Destination = destination.Trim(), VerificationReference = verificationReference.Trim(),
            Salt = salt, PasswordDigest = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32),
            Enabled = true, MustChangePassword = true
        };
    }

    public bool VerifyPassword(string password)
    {
        if (password is null || password.Length > 1024 || Salt.Length != 16 || PasswordDigest.Length != 32) return false;
        var candidate = Rfc2898DeriveBytes.Pbkdf2(password, Salt, Iterations, HashAlgorithmName.SHA256, 32);
        try { return CryptographicOperations.FixedTimeEquals(candidate, PasswordDigest); }
        finally { CryptographicOperations.ZeroMemory(candidate); }
    }

    public void Disable() => Enabled = false;
    public void Enable() => Enabled = true;

    public void ChangePassword(string password)
    {
        ValidatePasswordPolicy(password);
        if (VerifyPassword(password))
            throw new ArgumentException("La nueva contraseña debe ser diferente de la contraseña actual.", nameof(password));
        var salt = RandomNumberGenerator.GetBytes(16);
        Salt = salt;
        PasswordDigest = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        MustChangePassword = false;
    }

    public static void ValidatePasswordPolicy(string password)
    {
        if (password is null || password.Length < 8 || password.Length > 1024
            || !password.Any(char.IsUpper) || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit) || password.All(char.IsLetterOrDigit))
            throw new ArgumentException("La contraseña debe tener al menos 8 caracteres, una mayúscula, una minúscula, un número y un carácter especial.");
    }

    public void ConfirmEnrollmentEmail(string challengeReference)
    {
        if (Channel != VerificationChannel.InstitutionalEmail || !challengeReference.StartsWith("email-challenge:")
            || challengeReference.Length > 200) throw new ArgumentException("Verified email challenge required.");
        VerificationReference = challengeReference;
    }

    public string MaskedDestination => Channel == VerificationChannel.Sms
        ? "***" + Destination[^Math.Min(4, Destination.Length)..]
        : "***@" + (Destination.Contains('@') ? Destination.Split('@')[^1] : "***");
}

/// <summary>Action permission belongs to an institutional position, not directly to a person.</summary>
public sealed class PositionPermission
{
    public Guid OrganizationId { get; set; }
    public Guid PositionId { get; set; }
    public string Permission { get; set; } = "";
}
