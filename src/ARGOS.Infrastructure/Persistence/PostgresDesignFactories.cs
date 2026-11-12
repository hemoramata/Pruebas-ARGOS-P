using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ARGOS.Infrastructure.Persistence;

// Design-time only: never starts the API or connects to the institutional database.
internal static class PostgresDesignOptions
{
    internal static DbContextOptions<T> Create<T>() where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(
            "Host=localhost;Database=argos_design_only;Username=argos_design_only").Options;
}

public sealed class ArgosDesignFactory : IDesignTimeDbContextFactory<ArgosDbContext>
{
    public ArgosDbContext CreateDbContext(string[] args) => new(PostgresDesignOptions.Create<ArgosDbContext>());
}
public sealed class PersonnelDesignFactory : IDesignTimeDbContextFactory<PersonnelDbContext>
{
    public PersonnelDbContext CreateDbContext(string[] args) => new(PostgresDesignOptions.Create<PersonnelDbContext>());
}
public sealed class AccessDesignFactory : IDesignTimeDbContextFactory<AccessDbContext>
{
    public AccessDbContext CreateDbContext(string[] args) => new(PostgresDesignOptions.Create<AccessDbContext>());
}
public sealed class DesignationDesignFactory : IDesignTimeDbContextFactory<DesignationDbContext>
{
    public DesignationDbContext CreateDbContext(string[] args) => new(PostgresDesignOptions.Create<DesignationDbContext>());
}
