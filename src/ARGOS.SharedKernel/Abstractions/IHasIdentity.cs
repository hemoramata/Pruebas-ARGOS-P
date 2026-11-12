namespace ARGOS.SharedKernel.Abstractions;

/// <summary>
/// Define una entidad que posee un identificador fuertemente tipado.
/// Facilita repositorios genéricos, auditoría y utilidades comunes.
/// </summary>
public interface IHasIdentity<out TId>
{
    TId Id { get; }
}