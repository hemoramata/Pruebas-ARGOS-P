using System;

namespace ARGOS.SharedKernel.Extensions;

/// <summary>
/// Proporciona una fuente centralizada para obtener la fecha y hora UTC.
///
/// Permite sustituir temporalmente el proveedor de tiempo durante las
/// pruebas unitarias sin afectar el código de producción.
/// </summary>
public static class DateTimeProvider
{
    private static Func<DateTime> _utcNowProvider = () => DateTime.UtcNow;

    /// <summary>
    /// Obtiene la fecha y hora UTC actual.
    /// </summary>
    public static DateTime UtcNow => _utcNowProvider();

    /// <summary>
    /// Sustituye temporalmente el proveedor de fecha y hora.
    /// Uso exclusivo para pruebas.
    /// </summary>
    /// <param name="provider">Proveedor de fecha y hora.</param>
    public static void SetProvider(Func<DateTime> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _utcNowProvider = provider;
    }

    /// <summary>
    /// Restaura el proveedor por defecto basado en DateTime.UtcNow.
    /// </summary>
    public static void Reset()
    {
        _utcNowProvider = () => DateTime.UtcNow;
    }
}