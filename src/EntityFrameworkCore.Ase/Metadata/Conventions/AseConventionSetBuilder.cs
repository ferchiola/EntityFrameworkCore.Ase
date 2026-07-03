using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.Ase.Metadata.Conventions;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Agrega (Fase 5) <see cref="AseValueGenerationStrategyConvention" /> para que las propiedades
///     <c>int</c>/<c>long</c> con <see cref="ValueGenerated.OnAdd" /> (el caso típico de una PK
///     autoincremental) se marquen para generar columnas <c>IDENTITY</c> en las migraciones.
/// </remarks>
public class AseConventionSetBuilder : RelationalConventionSetBuilder
{
    public AseConventionSetBuilder(
        ProviderConventionSetBuilderDependencies dependencies,
        RelationalConventionSetBuilderDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        conventionSet.ModelFinalizingConventions.Add(
            new AseValueGenerationStrategyConvention(Dependencies, RelationalDependencies));

        return conventionSet;
    }
}
