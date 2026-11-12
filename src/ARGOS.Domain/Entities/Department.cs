using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa un departamento o dependencia perteneciente
/// a una institución.
/// </summary>
public class Department : Entity<Guid>
{
    private Department()
    {
    }

    public Department(
        Guid id,
        Institution institution,
        string name)
        : base(id)
    {
        Institution = institution ?? throw new ArgumentNullException(nameof(institution));
        Name = name.Trim();
        IsActive = true;
    }

    /// <summary>
    /// Institución a la que pertenece.
    /// </summary>
    public Institution Institution { get; private set; } = default!;

    /// <summary>
    /// Nombre del departamento.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Indica si el departamento está activo.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Cambia el nombre del departamento.
    /// </summary>
    public void Rename(string name)
    {
        Name = name.Trim();
    }

    /// <summary>
    /// Activa el departamento.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Desactiva el departamento.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}