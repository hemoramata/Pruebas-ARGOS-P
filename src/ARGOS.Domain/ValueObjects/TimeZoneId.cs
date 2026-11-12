using ARGOS.SharedKernel.Abstractions;
using ARGOS.SharedKernel.Results;
using System.Collections.Generic;

namespace ARGOS.Domain.ValueObjects;

/// <summary>
/// Representa una zona horaria válida utilizando identificadores IANA.
/// Ejemplos:
/// America/Costa_Rica
/// America/New_York
/// Europe/Madrid
/// Asia/Tokyo
/// UTC
/// </summary>
public sealed class TimeZoneId : ValueObject
{
    public string Value { get; }

    /// <summary>
    /// Obtiene el objeto TimeZoneInfo correspondiente.
    /// </summary>
    public TimeZoneInfo TimeZone =>
        TimeZoneInfo.FindSystemTimeZoneById(Value);

    private TimeZoneId(string value)
    {
        Value = value;
    }

    public static Result<TimeZoneId> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<TimeZoneId>.Failure(
                new Error(
                    "TimeZoneId.Required",
                    "La zona horaria es obligatoria."));
        }

        value = value.Trim();

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(value);

            return Result<TimeZoneId>.Success(
                new TimeZoneId(timeZone.Id));
        }
        catch (TimeZoneNotFoundException)
        {
            return Result<TimeZoneId>.Failure(
                new Error(
                    "TimeZoneId.Invalid",
                    "La zona horaria no existe."));
        }
        catch (InvalidTimeZoneException)
        {
            return Result<TimeZoneId>.Failure(
                new Error(
                    "TimeZoneId.Invalid",
                    "La zona horaria es inválida."));
        }
    }

    public static implicit operator string(TimeZoneId timeZoneId)
        => timeZoneId.Value;

    public override string ToString()
        => Value;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}