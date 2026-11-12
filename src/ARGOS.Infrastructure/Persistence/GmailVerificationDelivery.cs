using System.Net;
using System.Net.Mail;
using ARGOS.Application.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.Infrastructure.Persistence;

/// <summary>SMTP submission using Gmail STARTTLS. Successful submission is not proof of inbox delivery.</summary>
public sealed class GmailVerificationDelivery(IServiceScopeFactory scopes, Func<string?> sender,
    Func<string?> appPassword) : IVerificationDelivery
{
    public async Task SendAsync(Guid personId, VerificationChannel channel, string code, CancellationToken ct)
    {
        if (channel != VerificationChannel.InstitutionalEmail)
            throw new InvalidOperationException("Email delivery does not support SMS.");
        var from = sender();
        var secret = appPassword();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("Gmail verification delivery is not configured.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.PersonId == personId, ct);
        if (account is null || !account.Enabled || account.Channel != channel
            || string.IsNullOrWhiteSpace(account.VerificationReference))
            throw new InvalidOperationException("Verified email account is required.");
        await SendToEmailAsync(from, secret, account.Destination, code, ct);
    }

    public static async Task SendToEmailAsync(string from, string secret, string destination, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("Gmail is not configured.");
        using var message = CreateMessage(from, destination, code);
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(from, secret),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        try { await smtp.SendMailAsync(message, ct); }
        catch (SmtpException)
        {
            // Do not expose provider diagnostics containing addresses or authentication details.
            throw new InvalidOperationException("Verification email could not be submitted.");
        }
    }

    public static MailMessage CreateMessage(string sender, string destination, string code)
    {
        if (code is not { Length: 6 } || code.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Invalid verification code.");
        if (!MailAddress.TryCreate(sender, out var from) || from.Address != sender
            || !MailAddress.TryCreate(destination, out var to) || to.Address != destination)
            throw new InvalidOperationException("Invalid verification email address.");
        var message = new MailMessage(from, to)
        {
            Subject = "ARGOS - Código de verificación",
            Body = $"Su código de acceso a ARGOS es: {code}\n\nVence en un máximo de 3 minutos y solo puede usarse una vez.\nSi no solicitó este acceso, ignore el mensaje. No comparta el código.",
            IsBodyHtml = false,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        return message;
    }
}
