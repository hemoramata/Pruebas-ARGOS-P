namespace ARGOS.SharedKernel.Abstractions;

/// <summary>
/// Representa la clase base para todos los Value Objects del dominio.
///
/// Un Value Object se define únicamente por los valores que contiene,
/// no posee identidad propia y es inmutable por naturaleza.
///
/// Dos Value Objects son iguales cuando todos sus componentes de igualdad
/// poseen exactamente el mismo valor.
/// </summary>
public abstract class ValueObject :
    IEquatable<ValueObject>
{
    #region Equality

    /// <summary>
    /// Devuelve los componentes utilizados para determinar la igualdad.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc/>
    public bool Equals(ValueObject? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (other.GetType() != GetType())
            return false;

        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ValueObject);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }

    #endregion

    #region Debug

    /// <summary>
    /// Devuelve una representación amigable del Value Object.
    /// </summary>
    public override string ToString()
    {
        return $"{GetType().Name} {{ {string.Join(", ", GetEqualityComponents())} }}";
    }

    #endregion
}