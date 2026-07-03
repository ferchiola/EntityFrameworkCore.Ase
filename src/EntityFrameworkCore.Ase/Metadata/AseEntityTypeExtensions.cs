using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Ase.Metadata;

/// <summary>
///     Extension methods para leer anotaciones propias de Ase desde <see cref="IReadOnlyEntityType" />.
/// </summary>
public static class AseEntityTypeExtensions
{
    /// <summary>
    ///     Devuelve el esquema de locking configurado con <c>ForAseUseLockScheme</c>, o
    ///     <see langword="null" /> si no se configuró (en cuyo caso ASE usa su default de servidor).
    /// </summary>
    public static AseLockScheme? GetAseLockScheme(this IReadOnlyEntityType entityType)
        => (AseLockScheme?)entityType.FindAnnotation(AseAnnotationNames.LockScheme)?.Value;
}
