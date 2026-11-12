using ARGOS.SharedKernel.Results;

namespace ARGOS.Application.Modules.Procedures;

public static class ProcedureErrors
{
    public static readonly Error InvalidData = new(
        "Procedure.InvalidData",
        "El código, el nombre y la descripción son obligatorios.");

    public static readonly Error DuplicateCode = new(
        "Procedure.DuplicateCode",
        "Ya existe un procedimiento con el mismo código.");

    public static readonly Error NotFound = new(
        "Procedure.NotFound",
        "El procedimiento solicitado no existe.");

    public static readonly Error VersionConflict = new(
        "Procedure.VersionConflict",
        "No fue posible crear la versión porque ya existe una revisión concurrente.");

    public static readonly Error InvalidDecisionData = new(
        "Procedure.InvalidDecisionData",
        "La persona, asignación, posición, competencia, política y autorización son obligatorias.");
}
