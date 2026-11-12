using System.Data.Common;

namespace ARGOS.Server;
public static class DatabaseConfiguration
{
    public static string Build(string host, string port, string database, string user, string password)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Complete el equipo, la base, el usuario y la contraseña de PostgreSQL.");
        if (!int.TryParse(port, out var number) || number < 1 || number > 65535)
            throw new InvalidOperationException("El puerto PostgreSQL debe ser un número entre 1 y 65535.");
        var connection = new DbConnectionStringBuilder
        {
            ["Host"] = host.Trim(), ["Port"] = number, ["Database"] = database.Trim(),
            ["Username"] = user.Trim(), ["Password"] = password
        };
        return connection.ConnectionString;
    }
}
