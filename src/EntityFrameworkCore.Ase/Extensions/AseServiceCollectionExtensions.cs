using System.ComponentModel;
using EntityFrameworkCore.Ase.Diagnostics.Internal;
using EntityFrameworkCore.Ase.Infrastructure.Internal;
using EntityFrameworkCore.Ase.Metadata.Conventions;
using EntityFrameworkCore.Ase.Metadata.Internal;
using EntityFrameworkCore.Ase.Migrations;
using EntityFrameworkCore.Ase.Query.Internal;
using EntityFrameworkCore.Ase.Storage.Internal;
using EntityFrameworkCore.Ase.Update.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Update.Internal;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods específicos de Ase para <see cref="IServiceCollection" />.
/// </summary>
public static class AseServiceCollectionExtensions
{
    /// <summary>
    ///     Registra el <see cref="DbContext" /> dado como servicio y lo configura para conectarse a SAP ASE.
    /// </summary>
    public static IServiceCollection AddAse<TContext>(
        this IServiceCollection serviceCollection,
        string? connectionString,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null,
        Action<DbContextOptionsBuilder>? optionsAction = null)
        where TContext : DbContext
        => serviceCollection.AddDbContext<TContext>(
            (_, options) =>
            {
                optionsAction?.Invoke(options);
                options.UseAse(connectionString, aseOptionsAction);
            });

    /// <summary>
    ///     <para>
    ///         Agrega los servicios que necesita el provider de Ase para Entity Framework a un
    ///         <see cref="IServiceCollection" />.
    ///     </para>
    ///     <para>
    ///         Advertencia: no llamar a este método directamente salvo casos avanzados (ver
    ///         <see cref="DbContextOptionsBuilder.UseInternalServiceProvider" />) — es mucho más probable que
    ///         lo que necesites sea <see cref="AddAse{TContext}" />.
    ///     </para>
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IServiceCollection AddEntityFrameworkAse(this IServiceCollection serviceCollection)
    {
        new EntityFrameworkRelationalServicesBuilder(serviceCollection)
            .TryAdd<LoggingDefinitions, AseLoggingDefinitions>()
            .TryAdd<IDatabaseProvider, DatabaseProvider<AseOptionsExtension>>()

            // --- Fase 2 (Conexión) ---
            .TryAdd<IRelationalConnection>(p => p.GetRequiredService<IAseConnection>())

            // --- Fase 3 (Type Mapping) ---
            .TryAdd<IRelationalTypeMappingSource, AseTypeMappingSource>()

            // --- Fase 4 (SQL Generation / queries) ---
            .TryAdd<ISqlGenerationHelper, AseSqlGenerationHelper>()
            .TryAdd<IQuerySqlGeneratorFactory, AseQuerySqlGeneratorFactory>()
            .TryAdd<IMethodCallTranslatorProvider, AseMethodCallTranslatorProvider>()
            .TryAdd<IMemberTranslatorProvider, AseMemberTranslatorProvider>()
            // Se usa la clase genérica de EF Core (RelationalQueryableMethodTranslatingExpressionVisitorFactory)
            // tal cual — no hace falta una propia todavía, no hay ninguna forma de query shape que ASE
            // resuelva distinto (ver DECISIONS.md, Fase 4).
            .TryAdd<IQueryableMethodTranslatingExpressionVisitorFactory, RelationalQueryableMethodTranslatingExpressionVisitorFactory>()
            .TryAdd<IRelationalSqlTranslatingExpressionVisitorFactory, AseSqlTranslatingExpressionVisitorFactory>()

            // --- No pedidas explícitamente en ninguna fase, pero necesarias para que el árbol de
            // servicios de DbContext se resuelva siquiera para hacer una query de lectura (EF Core arma
            // todo el árbol, incluida la parte de escritura, de entrada) — ver DECISIONS.md, Fase 4.
            // RelationalModelValidator y RelationalValueGeneratorSelector son clases genéricas de EF
            // Core ya utilizables tal cual, sin necesidad de una subclase propia.
            .TryAdd<IModelValidator, RelationalModelValidator>()
            .TryAdd<IProviderConventionSetBuilder, AseConventionSetBuilder>()
            .TryAdd<IValueGeneratorSelector, RelationalValueGeneratorSelector>()
            .TryAdd<IUpdateSqlGenerator, AseUpdateSqlGenerator>()
            .TryAdd<IRelationalDatabaseCreator, AseDatabaseCreator>()
            .TryAdd<IModificationCommandFactory, ModificationCommandFactory>()
            .TryAdd<IModificationCommandBatchFactory, AseModificationCommandBatchFactory>()

            // --- Fase 5 (Migraciones) --- (IRelationalDatabaseCreator ya se registró arriba, con la
            // implementación real de esta fase reemplazando el placeholder de la Fase 4)
            .TryAdd<IMigrationsSqlGenerator, AseMigrationsSqlGenerator>()
            .TryAdd<IHistoryRepository, AseHistoryRepository>()

            // --- Fase 6 (Metadata / Annotations) ---
            // IRelationalAnnotationProvider es lo que popula ITable.GetAnnotations() al armar el
            // modelo relacional en tiempo de ejecución — MigrationsModelDiffer copia esas anotaciones
            // tal cual a CreateTableOperation (confirmado leyendo el código fuente de
            // MigrationsModelDiffer.Add(ITable, ...) en dotnet/efcore), así que es el único servicio
            // que hace falta para que ForAseUseLockScheme llegue hasta el SQL generado.
            .TryAdd<IRelationalAnnotationProvider, AseAnnotationProvider>()

            .TryAddProviderSpecificServices(
                b => b.TryAddScoped<IAseConnection, AseRelationalConnection>())
            .TryAddCoreServices();

        return serviceCollection;
    }
}
