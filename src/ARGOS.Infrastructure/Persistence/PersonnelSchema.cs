using Microsoft.EntityFrameworkCore;

namespace ARGOS.Infrastructure.Persistence;

public static class PersonnelSchema
{
    /// <summary>
    /// Applies the versioned PostgreSQL personnel migrations without recreating existing tables.
    /// </summary>
    public static Task InitializeAsync(PersonnelDbContext context, CancellationToken cancellationToken = default)
        => context.Database.MigrateAsync(cancellationToken);
}
