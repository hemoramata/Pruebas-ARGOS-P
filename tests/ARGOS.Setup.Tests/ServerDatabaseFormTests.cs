using System;
using System.Data.Common;
using ARGOS.Server;

public class ServerDatabaseFormTests
{
    [Fact]
    public void PreservesSpecialCharactersWithoutInjectingConnectionOptions()
    {
        const string password = "synthetic;Database=wrong;\" value ";
        var result = DatabaseConfiguration.Build(" localhost ", "5432", " test ", " user ", password);
        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        Assert.Equal("test", parsed["Database"]);
        Assert.Equal(password, parsed["Password"]);
        Assert.Equal(5, parsed.Count);
        Assert.StartsWith("Host=", result, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("texto")]
    public void RejectsInvalidPort(string port)
        => Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.Build("localhost", port, "test", "user", "synthetic"));
    [Fact]
    public void RequiresPassword()
        => Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.Build("localhost", "5432", "test", "user", ""));
}
