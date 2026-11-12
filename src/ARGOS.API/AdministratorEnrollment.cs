using System.Globalization;
using System.Net.Mail;
using ARGOS.Application.Common.Persistence;
using ARGOS.Application.Security;
using ARGOS.Domain.Aggregates.Personnel;
using ARGOS.Infrastructure.Persistence;
using Npgsql;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API;

/// <summary>Interactive server-local enrollment; never exposed as an anonymous HTTP endpoint.</summary>
public static class AdministratorEnrollment
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, string connectionString)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("El alta requiere una consola interactiva en el servidor.");
        var stage = EnrollmentStage.Preparation;
        try
        {
            await using var scope = services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOrganizationSetupRepository>();
            var current = await repository.GetCurrentAsync();
            if (current is null)
            {
                Console.WriteLine("No existe configuracion institucional. Abra /setup/ en esta instalacion, complete y publique la estructura antes de registrar al administrador.");
                Environment.ExitCode = 1;
                return;
            }
            var setup = await repository.GetPublishedAsync(current.OrganizationId);
            if (setup is null)
            {
                Console.WriteLine("La configuracion institucional todavia no esta publicada. Complete y publique /setup/ antes del alta. No se creo ninguna cuenta.");
                Environment.ExitCode = 1;
                return;
            }
            var sender = configuration["Gmail:Sender"];
            var secret = configuration["Gmail:AppPassword"];
            if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(secret))
            {
                Console.WriteLine("Falta configurar el remitente o la contrasena de aplicacion Gmail en este proceso. No se creo ninguna cuenta.");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine("Alta de administrador designado por la institución. Ctrl+C cancela sin registrar la cuenta.");
            var positions = setup.Positions.ToArray();
            if (positions.Length == 0)
                throw new InvalidOperationException("No published positions.");
            stage = EnrollmentStage.Data;
            for (var i = 0; i < positions.Length; i++)
                Console.WriteLine($"{i + 1}. {positions[i].Code} — {positions[i].Name}");
            int selection;
            while (!int.TryParse(Read("Número del puesto designado"), out selection)
                || selection < 1 || selection > positions.Length)
                Console.WriteLine("Seleccione un numero de la lista. No se han descartado sus datos.");
            var fullName = Read("Nombre completo de la persona");
            var issuer = Read("Emisor del documento de identidad");
            var document = Read("Tipo de documento");
            var number = Read("Número de documento");
            var identification = number;
            Console.WriteLine("El acceso sera con el numero del documento de identidad registrado.");
            var authorization = Read("Referencia del acuerdo o autorización institucional");
            var authority = Read("Autoridad institucional que aprobó la designación");
            var registrar = Read("Responsable que registra esta designación");
            var input = new EnrollmentInput(Console.In, Console.Out);
            var start = input.ReadDate("Inicio de vigencia (AAAA-MM-DD o DD/MM/AAAA; hora local)")!.Value;
            var end = input.ReadDate("Fin de vigencia exclusivo (AAAA-MM-DD o DD/MM/AAAA; vacio si no tiene fin)", optional: true, after: start);
            string email;
            while (true)
            {
                email = Read("Correo del administrador que se verificará");
                if (MailAddress.TryCreate(email, out var address) && address.Address == email) break;
                Console.WriteLine("Correo no valido. Corrija solamente este dato.");
            }
            string password;
            while (true)
            {
                password = ReadSecret("Clave temporal inicial ARGOS (mayuscula, minuscula, numero y simbolo; minimo 8 caracteres)");
                try { AccessAccount.ValidatePasswordPolicy(password); }
                catch (ArgumentException error) { Console.WriteLine(error.Message); password = ""; continue; }
                if (password == ReadSecret("Repita la contraseña ARGOS")) break;
                password = "";
                Console.WriteLine("Las claves no coinciden. Vuelva a introducirlas; los otros datos se conservan.");
            }
            var person = new InstitutionalPerson(setup.OrganizationId, new OfficialIdentity(issuer, document, number), fullName);
            var assignment = person.Assign(setup, positions[selection - 1].Id, start, end, authorization);
            var designation = AdministratorDesignation.Create(person, assignment.Id, authorization, authority, registrar,
                DateTimeOffset.UtcNow, start, end);
            // Validate credentials before issuing mail; no account is persisted until possession is proven.
            var account = AccessAccount.Provision(person.Id, identification, password,
                VerificationChannel.InstitutionalEmail, email, "pending");
            password = "";
            Console.WriteLine($"Se registrará a {fullName} en el puesto {positions[selection - 1].Name}.");
            if (Read("Escriba REGISTRAR para confirmar la designación y enviar el código") != "REGISTRAR") return;
            var binding = Guid.NewGuid();
            stage = EnrollmentStage.SendingEmail;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var challenge = await SecondFactorChallenge.IssueAsync(person.Id, binding, "enrollment",
                VerificationChannel.InstitutionalEmail, TimeSpan.FromMinutes(3), 5,
                (code, ct) => GmailVerificationDelivery.SendToEmailAsync(sender, secret, email, code, ct),
                cancellationToken: timeout.Token);
            stage = EnrollmentStage.VerifyingEmail;
            Console.WriteLine("Codigo enviado. Revise el buzon y correo no deseado; vence en tres minutos. No comparta el codigo.");
            var result = VerificationResult.Rejected;
            while (result == VerificationResult.Rejected)
                result = challenge.Verify(person.Id, binding, "enrollment", ReadSecret("Código recibido por correo"));
            if (result != VerificationResult.Accepted) throw new InvalidOperationException("Correo no verificado. No se registró la cuenta.");
            account.ConfirmEnrollmentEmail($"email-challenge:{challenge.Id}");
            stage = EnrollmentStage.Saving;
            await SaveAsync(connectionString, person, designation, account);
            Console.WriteLine("Administrador registrado y correo verificado. Puede iniciar sesión durante la vigencia autorizada.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or DbUpdateException
            or NpgsqlException or OperationCanceledException or TimeoutException or FormatException)
        {
            Console.WriteLine(EnrollmentDiagnostics.Describe(stage));
            Environment.ExitCode = 1;
        }
    }

    public static async Task SaveAsync(string connectionString, InstitutionalPerson person,
        AdministratorDesignation designation, AccessAccount account)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var personnel = new PersonnelDbContext(new DbContextOptionsBuilder<PersonnelDbContext>().UseNpgsql(connection).Options);
        await using var access = new AccessDbContext(new DbContextOptionsBuilder<AccessDbContext>().UseNpgsql(connection).Options);
        await using var designations = new DesignationDbContext(new DbContextOptionsBuilder<DesignationDbContext>().UseNpgsql(connection).Options);
        await using var organizations = new ArgosDbContext(new DbContextOptionsBuilder<ArgosDbContext>().UseNpgsql(connection).Options);
        await personnel.Database.UseTransactionAsync(transaction);
        await access.Database.UseTransactionAsync(transaction);
        await designations.Database.UseTransactionAsync(transaction);
        await organizations.Database.UseTransactionAsync(transaction);
        var setup = await new PostgresOrganizationSetupRepository(organizations).GetPublishedAsync(person.OrganizationId);
        var assignment = person.Assignments.Single(a => a.Id == designation.AssignmentId);
        if (setup is null || !setup.Positions.Any(p => p.Id == assignment.PositionId)
            || designation.PersonId != person.Id || designation.OrganizationId != person.OrganizationId.Value
            || account.PersonId != person.Id || !account.VerificationReference.StartsWith("email-challenge:"))
            throw new InvalidOperationException("Enrollment context is no longer valid.");
        personnel.People.Add(person);
        access.Accounts.Add(account);
        designations.Designations.Add(designation);
        if (!await access.PositionPermissions.AnyAsync(p => p.OrganizationId == person.OrganizationId.Value
            && p.PositionId == assignment.PositionId && p.Permission == "personnel.manage"))
            access.PositionPermissions.Add(new PositionPermission { OrganizationId = person.OrganizationId.Value,
                PositionId = assignment.PositionId, Permission = "personnel.manage" });
        await personnel.SaveChangesAsync();
        await access.SaveChangesAsync();
        await designations.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static string Read(string label, bool optional = false)
        => new EnrollmentInput(Console.In, Console.Out).Read(label, optional);
    private static string ReadSecret(string label)
    {
        Console.Write($"{label}: ");
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; }
            else if (!char.IsControl(key.KeyChar) && buffer.Length < 1024) buffer.Append(key.KeyChar);
        }
        Console.WriteLine();
        return buffer.ToString();
    }
}
