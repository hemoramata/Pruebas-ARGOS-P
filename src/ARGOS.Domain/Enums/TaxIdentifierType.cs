namespace ARGOS.Domain.Enums;

/// <summary>
/// Representa los tipos de identificadores oficiales soportados por ARGOS.
/// </summary>
public enum TaxIdentifierType
{
    /// <summary>
    /// Tipo de identificación no determinado.
    /// Se utiliza como valor por defecto o cuando aún no ha sido posible
    /// identificar el tipo del documento.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Cédula de persona física costarricense.
    /// </summary>
    NaturalPerson = 1,

    /// <summary>
    /// Cédula jurídica costarricense.
    /// </summary>
    LegalEntity = 2,

    /// <summary>
    /// Documento de Identidad Migratorio para Extranjeros (DIMEX).
    /// </summary>
    DIMEX = 3,

    /// <summary>
    /// Número de Identificación Tributaria Especial (NITE).
    /// </summary>
    NITE = 4
}