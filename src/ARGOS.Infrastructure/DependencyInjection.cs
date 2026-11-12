using ARGOS.Application.Common.Persistence;
using ARGOS.Application.Security;
using ARGOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ARGOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (!connectionString.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ARGOS requiere una cadena de conexión PostgreSQL (Host=...).", nameof(connectionString));
        services.AddDbContext<ArgosDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IProcedureRepository, PostgresProcedureRepository>();
        services.AddScoped<PostgresWorkflowLoader>();
        services.AddScoped<PostgresCaseWorkflowService>();
        services.AddDbContext<PersonnelDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContext<AccessDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ISensitiveDecisionStore, PostgresSensitiveDecisionStore>();
        services.AddScoped<SensitiveDecisionService>();
        services.AddDbContext<DesignationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<PostgresLoginAccounts>();
        services.AddSingleton<IPasswordRecoveryAccounts, PostgresPasswordRecovery>();
        services.AddSingleton<ARGOS.Application.Security.ILoginAccounts>(sp => sp.GetRequiredService<PostgresLoginAccounts>());
        services.AddSingleton<ARGOS.Application.Security.IPasswordChanger>(sp => sp.GetRequiredService<PostgresLoginAccounts>());
        services.AddScoped<IOrganizationSetupRepository, PostgresOrganizationSetupRepository>();

        return services;
    }

}
