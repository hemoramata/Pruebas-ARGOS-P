using ARGOS.Domain.Aggregates.Organization;
using ARGOS.Domain.Aggregates.Organization.ValueObjects;
using ARGOS.Domain.Aggregates.OrganizationSetup;
using ARGOS.Domain.Aggregates.OrganizationSetup.ValueObjects;

namespace ARGOS.Application.Common.Persistence;

public interface IOrganizationSetupRepository
{
    Task<OrganizationSetup?> GetCurrentAsync(
        CancellationToken cancellationToken = default);

    Task<Organization?> GetOrganizationAsync(
        OrganizationId id,
        CancellationToken cancellationToken = default);

    Task<OrganizationSetup?> GetByIdAsync(
        OrganizationSetupId id,
        CancellationToken cancellationToken = default);

    Task<OrganizationSetup?> GetPublishedAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrganizationSetup>> ListVersionsAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddInitialAsync(
        Organization organization,
        OrganizationSetup setup,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddVersionAsync(
        OrganizationSetup currentVersion,
        OrganizationSetup newVersion,
        CancellationToken cancellationToken = default);

    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
}
