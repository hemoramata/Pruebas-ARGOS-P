using ARGOS.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.UnitTests;

public sealed class PostgresOnlyConfigurationTests
{
    [Theory]
    [InlineData("Data Source=argos.db")]
    [InlineData("Filename=unsupported.db")]
    [InlineData("")]
    public void InfrastructureRejectsNonPostgresConnectionStrings(string connectionString)
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.AddInfrastructure(connectionString));
    }

    [Fact]
    public void InfrastructureAcceptsPostgresConnectionStringWithoutOpeningIt()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure("Host=127.0.0.1;Port=5432;Database=ARGOS;Username=postgres");
        Assert.NotEmpty(services);
    }
}
