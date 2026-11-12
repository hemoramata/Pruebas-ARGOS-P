using System.Globalization;

namespace ARGOS.API;

/// <summary>Retry recoverable input errors without discarding the enrollment in progress.</summary>
public sealed class EnrollmentInput(TextReader input, TextWriter output)
{
    public string Read(string label, bool optional = false)
    {
        while (true)
        {
            output.Write($"{label}: ");
            var line = input.ReadLine();
            if (line is null) throw new OperationCanceledException("Se cerro la entrada del alta.");
            var value = line.Trim();
            if (optional || value.Length > 0) return value;
            output.WriteLine("Este dato es obligatorio. Puede corregirlo sin reiniciar el alta.");
        }
    }

    public DateTimeOffset? ReadDate(string label, bool optional = false, DateTimeOffset? after = null)
    {
        while (true)
        {
            var value = Read(label, optional);
            if (optional && value.Length == 0) return null;
            if (!DateTime.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            {
                output.WriteLine("Fecha no valida. Use AAAA-MM-DD o DD/MM/AAAA; por ejemplo, 2025-10-24 o 24/10/2025.");
                continue;
            }
            var result = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local));
            if (after.HasValue && result <= after.Value)
            {
                output.WriteLine("La fecha final debe ser posterior al inicio. Corrija la fecha o dejela vacia si no tiene fin.");
                continue;
            }
            return result;
        }
    }
}
