namespace ARGOS.SharedKernel.ValueObjects;

/// <summary>
/// Base class for all strongly typed entity identifiers.
/// </summary>
public abstract class EntityId : IEquatable<EntityId>
{
    /// <summary>
    /// Underlying Guid value.
    /// </summary>
    public Guid Value { get; }

    protected EntityId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("The entity identifier cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString()
        => Value.ToString();

    public override bool Equals(object? obj)
        => Equals(obj as EntityId);

    public bool Equals(EntityId? other)
    {
        if (other is null)
            return false;

        return GetType() == other.GetType()
            && Value == other.Value;
    }

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Value);

    public static bool operator ==(EntityId? left, EntityId? right)
        => Equals(left, right);

    public static bool operator !=(EntityId? left, EntityId? right)
        => !Equals(left, right);

    public static implicit operator Guid(EntityId id)
        => id.Value;
}