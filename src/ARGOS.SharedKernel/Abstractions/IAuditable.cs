namespace ARGOS.SharedKernel.Abstractions;

public interface IAuditable
{
    DateTime CreatedAt { get; }

    string CreatedBy { get; }

    DateTime? UpdatedAt { get; }

    string? UpdatedBy { get; }
}