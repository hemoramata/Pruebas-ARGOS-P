using ARGOS.SharedKernel.Results;

namespace ARGOS.Application.Modules.OrganizationSetup;

public static class OrganizationSetupApplicationErrors
{
    public static readonly Error InvalidOrganization = new(
        "OrganizationSetup.InvalidOrganization",
        "El nombre y el código de la institución son obligatorios y deben respetar sus longitudes máximas.");

    public static readonly Error InvalidAction = new(
        "OrganizationSetup.InvalidAction",
        "La persona responsable, la versión de la política y la autorización son obligatorias.");

    public static readonly Error NotFound = new(
        "OrganizationSetup.NotFound",
        "La configuración organizacional solicitada no existe.");

    public static readonly Error InstallationAlreadyExists = new(
        "OrganizationSetup.InstallationAlreadyExists",
        "Esta instalación de ARGOS ya pertenece a una institución.");

    public static readonly Error PersistenceConflict = new(
        "OrganizationSetup.PersistenceConflict",
        "La configuración cambió simultáneamente o contiene un código duplicado; vuelva a cargarla e inténtelo de nuevo.");
}
