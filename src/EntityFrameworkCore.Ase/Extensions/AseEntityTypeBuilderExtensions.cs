using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Extension methods específicos de Ase para <see cref="EntityTypeBuilder" />.
/// </summary>
public static class AseEntityTypeBuilderExtensions
{
    /// <summary>
    ///     Configura el esquema de locking (<c>LOCK ALLPAGES</c>/<c>DATAPAGES</c>/<c>DATAROWS</c>) que
    ///     ASE va a usar para la tabla mapeada a esta entidad. No tiene equivalente en SQL Server — es
    ///     una feature propia de ASE (ver DECISIONS.md, Fase 6).
    /// </summary>
    public static EntityTypeBuilder ForAseUseLockScheme(
        this EntityTypeBuilder entityTypeBuilder,
        AseLockScheme lockScheme)
    {
        entityTypeBuilder.Metadata.SetAnnotation(AseAnnotationNames.LockScheme, lockScheme);
        return entityTypeBuilder;
    }

    /// <inheritdoc cref="ForAseUseLockScheme(EntityTypeBuilder, AseLockScheme)" />
    public static EntityTypeBuilder<TEntity> ForAseUseLockScheme<TEntity>(
        this EntityTypeBuilder<TEntity> entityTypeBuilder,
        AseLockScheme lockScheme)
        where TEntity : class
        => (EntityTypeBuilder<TEntity>)ForAseUseLockScheme((EntityTypeBuilder)entityTypeBuilder, lockScheme);
}
