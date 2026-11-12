namespace ARGOS.Infrastructure.Persistence;

/// <summary>Clasificación documental definida por cada institución, no por el modelo de IA.</summary>
public sealed class DocumentCategory
{
    private DocumentCategory() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static DocumentCategory Create(Guid organizationId, string code, string name, string? description, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId,
            Code = Required(code, 50).ToUpperInvariant(), Name = Required(name, 150),
            Description = Optional(description, 1000), IsActive = true, CreatedAtUtc = now
        };

    public void Deactivate() => IsActive = false;

    private static string Required(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("El valor es obligatorio.");
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentException("El valor excede el tamaño permitido.");
        return result;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > max) throw new ArgumentException("El valor excede el tamaño permitido.");
        return result;
    }
}
