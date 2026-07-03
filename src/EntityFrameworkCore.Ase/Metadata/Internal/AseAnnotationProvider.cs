using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Ase.Metadata.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Sin esto, una anotación puesta con <c>ForAseUseLockScheme</c> en el <see cref="ModelBuilder" />
///     queda en el modelo de EF Core pero nunca llega a <c>CreateTableOperation</c> — las migraciones
///     leen anotaciones a nivel tabla/columna a través de este provider, no directamente del modelo
///     (mismo patrón que usa <c>SqlServerAnnotationProvider</c> para <c>MemoryOptimized</c>).
/// </remarks>
public class AseAnnotationProvider : RelationalAnnotationProvider
{
    public AseAnnotationProvider(RelationalAnnotationProviderDependencies dependencies)
        : base(dependencies)
    {
    }

    public override IEnumerable<IAnnotation> For(ITable table, bool designTime)
    {
        foreach (var annotation in base.For(table, designTime))
        {
            yield return annotation;
        }

        if (!designTime)
        {
            yield break;
        }

        var lockScheme = table.EntityTypeMappings
            .Select(m => ((IEntityType)m.TypeBase).GetAseLockScheme())
            .FirstOrDefault(scheme => scheme != null);

        if (lockScheme != null)
        {
            yield return new Annotation(AseAnnotationNames.LockScheme, lockScheme.Value);
        }
    }

    /// <remarks>
    ///     Sin esto, <c>AseValueGenerationStrategyConvention</c> (Fase 5) marca la propiedad como
    ///     <c>IdentityColumn</c> en el modelo, pero esa anotación nunca llegaba a
    ///     <c>ColumnOperation</c> — el único motivo por el que el test de la Fase 5 funcionaba era que
    ///     seteaba la anotación a mano directamente en la migración escrita a mano, sin pasar por acá.
    ///     Se detectó al agregar un test de Fase 6 que sí usa <c>EnsureCreated()</c>/convenciones de
    ///     punta a punta (ver DECISIONS.md, Fase 6): la columna se creaba <c>NOT NULL</c> normal en vez
    ///     de <c>IDENTITY</c>.
    /// </remarks>
    public override IEnumerable<IAnnotation> For(IColumn column, bool designTime)
    {
        foreach (var annotation in base.For(column, designTime))
        {
            yield return annotation;
        }

        if (!designTime)
        {
            yield break;
        }

        var strategy = column.PropertyMappings
            .Select(m => (AseValueGenerationStrategy?)m.Property[AseAnnotationNames.ValueGenerationStrategy])
            .FirstOrDefault(s => s != null);

        if (strategy != null)
        {
            yield return new Annotation(AseAnnotationNames.ValueGenerationStrategy, strategy.Value);
        }
    }
}
