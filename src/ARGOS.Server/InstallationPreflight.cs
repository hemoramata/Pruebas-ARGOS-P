using System.Net.NetworkInformation;
using Npgsql;

namespace ARGOS.Server;
public sealed class InstallationPreflightException(string message) : Exception(message);

public static class InstallationPreflight
{
    public static void CheckPort(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new InstallationPreflightException("Revise la dirección HTTPS del servidor.");
        // Conservative: do not interfere with another local listener, even on a different interface.
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == uri.Port))
            throw new InstallationPreflightException("El puerto HTTPS está ocupado. Seleccione otro puerto o revise la instancia existente; no se detuvo ningún proceso.");
    }

    public static async Task CheckDatabaseAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString)
            { Timeout = 5, CommandTimeout = 5, Pooling = false, IncludeErrorDetail = false };
            if (string.IsNullOrWhiteSpace(settings.Database))
                throw new InstallationPreflightException("Indique expresamente el nombre de la base de datos.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(deadline.Token);
            await using var command = new NpgsqlCommand("SELECT current_database()", connection);
            var actual = await command.ExecuteScalarAsync(deadline.Token) as string;
            if (!string.Equals(actual, settings.Database, StringComparison.Ordinal))
                throw new InstallationPreflightException("La base conectada no coincide con la seleccionada. No se inició la instalación.");
        }
        catch (InstallationPreflightException) { throw; }
        catch
        {
            // Provider errors can contain server details: return only a safe actionable message.
            throw new InstallationPreflightException("No se pudo comprobar PostgreSQL. Revise equipo, puerto, base, usuario y contraseña. No se creó el servicio ni se modificó la base.");
        }
    }
}
