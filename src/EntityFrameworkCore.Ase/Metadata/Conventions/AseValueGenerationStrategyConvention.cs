using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Versión simplificada de <c>SqlServerValueGenerationStrategyConvention</c> (sin soporte de
///     secuencias, sin distinguir tablas de vistas) — marca como <c>IDENTITY</c> cualquier propiedad
///     con <see cref="ValueGenerated.OnAdd" />, tipo entero, sin default value/computed column. Cubre
///     el caso típico (PK <c>int</c>/<c>long</c> autoincremental) que es el que pide la Fase 5.
/// </remarks>
public class AseValueGenerationStrategyConvention : IModelFinalizingConvention
{
    public AseValueGenerationStrategyConvention(
        ProviderConventionSetBuilderDependencies dependencies,
        RelationalConventionSetBuilderDependencies relationalDependencies)
    {
    }

    public virtual void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var property in entityType.GetDeclaredProperties())
            {
                if (property.ValueGenerated == ValueGenerated.OnAdd
                    && IsIntegerType(property.ClrType)
                    && property.GetDefaultValueSql() == null
                    && property.GetComputedColumnSql() == null
                    && property.TryGetDefaultValue(out _) == false)
                {
                    property.Builder.HasAnnotation(
                        AseAnnotationNames.ValueGenerationStrategy,
                        AseValueGenerationStrategy.IdentityColumn);
                }
            }
        }
    }

    private static bool IsIntegerType(Type clrType)
    {
        var type = clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(Nullable<>)
            ? Nullable.GetUnderlyingType(clrType)!
            : clrType;

        return type == typeof(int) || type == typeof(long) || type == typeof(short);
    }
}
