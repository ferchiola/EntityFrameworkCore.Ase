using System.Data.Common;
using EntityFrameworkCore.Ase.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Extension methods específicos de Ase para <see cref="DbContextOptionsBuilder" />.
/// </summary>
public static class AseDbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Configura el contexto para conectarse a una base SAP ASE, sin fijar todavía connection string
    ///     ni <see cref="DbConnection" />. Hay que setear alguna de las dos antes de usar el contexto.
    /// </summary>
    public static DbContextOptionsBuilder UseAse(
        this DbContextOptionsBuilder optionsBuilder,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
    {
        var extension = GetOrCreateExtension(optionsBuilder);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return ApplyConfiguration(optionsBuilder, aseOptionsAction);
    }

    /// <summary>
    ///     Configura el contexto para conectarse a una base SAP ASE usando el connection string dado.
    /// </summary>
    public static DbContextOptionsBuilder UseAse(
        this DbContextOptionsBuilder optionsBuilder,
        string? connectionString,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
    {
        var extension = (AseOptionsExtension)GetOrCreateExtension(optionsBuilder)
            .WithConnectionString(connectionString);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return ApplyConfiguration(optionsBuilder, aseOptionsAction);
    }

    /// <summary>
    ///     Configura el contexto para conectarse a una base SAP ASE usando una <see cref="DbConnection" />
    ///     ya existente. El caller sigue siendo dueño de la conexión y responsable de disponerla.
    /// </summary>
    public static DbContextOptionsBuilder UseAse(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
        => UseAse(optionsBuilder, connection, contextOwnsConnection: false, aseOptionsAction);

    /// <summary>
    ///     Configura el contexto para conectarse a una base SAP ASE usando una <see cref="DbConnection" />
    ///     ya existente.
    /// </summary>
    public static DbContextOptionsBuilder UseAse(
        this DbContextOptionsBuilder optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var extension = (AseOptionsExtension)GetOrCreateExtension(optionsBuilder)
            .WithConnection(connection, contextOwnsConnection);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return ApplyConfiguration(optionsBuilder, aseOptionsAction);
    }

    /// <inheritdoc cref="UseAse(DbContextOptionsBuilder, Action{AseDbContextOptionsBuilder}?)" />
    public static DbContextOptionsBuilder<TContext> UseAse<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseAse((DbContextOptionsBuilder)optionsBuilder, aseOptionsAction);

    /// <inheritdoc cref="UseAse(DbContextOptionsBuilder, string?, Action{AseDbContextOptionsBuilder}?)" />
    public static DbContextOptionsBuilder<TContext> UseAse<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        string? connectionString,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseAse((DbContextOptionsBuilder)optionsBuilder, connectionString, aseOptionsAction);

    /// <inheritdoc cref="UseAse(DbContextOptionsBuilder, DbConnection, Action{AseDbContextOptionsBuilder}?)" />
    public static DbContextOptionsBuilder<TContext> UseAse<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseAse((DbContextOptionsBuilder)optionsBuilder, connection, aseOptionsAction);

    /// <inheritdoc cref="UseAse(DbContextOptionsBuilder, DbConnection, bool, Action{AseDbContextOptionsBuilder}?)" />
    public static DbContextOptionsBuilder<TContext> UseAse<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DbConnection connection,
        bool contextOwnsConnection,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseAse((DbContextOptionsBuilder)optionsBuilder, connection, contextOwnsConnection, aseOptionsAction);

    private static AseOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<AseOptionsExtension>() ?? new AseOptionsExtension();

    private static DbContextOptionsBuilder ApplyConfiguration(
        DbContextOptionsBuilder optionsBuilder,
        Action<AseDbContextOptionsBuilder>? aseOptionsAction)
    {
        aseOptionsAction?.Invoke(new AseDbContextOptionsBuilder(optionsBuilder));

        var extension = (AseOptionsExtension)GetOrCreateExtension(optionsBuilder).ApplyDefaults(optionsBuilder.Options);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        return optionsBuilder;
    }
}
