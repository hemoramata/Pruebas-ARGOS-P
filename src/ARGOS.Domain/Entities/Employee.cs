using ARGOS.SharedKernel.Abstractions;

namespace ARGOS.Domain.Entities;

/// <summary>
/// Representa a un funcionario perteneciente a un departamento.
/// </summary>
public class Employee : Entity<Guid>
{
    private Employee()
    {
    }

    public Employee(
        Guid id,
        Citizen citizen,
        Department department,
        string position,
        DateOnly hireDate)
        : base(id)
    {
        Citizen = citizen ?? throw new ArgumentNullException(nameof(citizen));
        Department = department ?? throw new ArgumentNullException(nameof(department));
        Position = position.Trim();
        HireDate = hireDate;
        IsActive = true;
    }

    /// <summary>
    /// Persona asociada al funcionario.
    /// </summary>
    public Citizen Citizen { get; private set; } = default!;

    /// <summary>
    /// Departamento al que pertenece.
    /// </summary>
    public Department Department { get; private set; } = default!;

    /// <summary>
    /// Cargo desempeñado.
    /// </summary>
    public string Position { get; private set; } = string.Empty;

    /// <summary>
    /// Fecha de contratación.
    /// </summary>
    public DateOnly HireDate { get; private set; }

    /// <summary>
    /// Indica si el funcionario está activo.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Cambia el departamento.
    /// </summary>
    public void ChangeDepartment(Department department)
    {
        Department = department ?? throw new ArgumentNullException(nameof(department));
    }

    /// <summary>
    /// Cambia el cargo.
    /// </summary>
    public void ChangePosition(string position)
    {
        Position = position.Trim();
    }

    /// <summary>
    /// Activa al funcionario.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>
    /// Desactiva al funcionario.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}