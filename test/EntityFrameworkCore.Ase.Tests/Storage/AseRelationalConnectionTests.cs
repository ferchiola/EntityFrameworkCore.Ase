using AdoNetCore.AseClient;
using EntityFrameworkCore.Ase.Storage.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Moq;
using Xunit;

namespace EntityFrameworkCore.Ase.Tests.Storage;

public class AseRelationalConnectionTests
{
    private const string ConnectionString = "Data Source=localhost;Port=5000;Database=Dummy;Uid=sa;Pwd=dummy;Pooling=true;Max Pool Size=50;";

    [Fact]
    public void CreateDbConnection_returns_an_AseConnection_with_the_configured_connection_string()
    {
        using var connection = new AseRelationalConnection(CreateDependencies());

        Assert.IsType<AseConnection>(connection.DbConnection);
        Assert.Equal(ConnectionString, connection.DbConnection.ConnectionString);
    }

    [Fact]
    public void ConnectionString_round_trips_through_the_connection()
        => Assert.Equal(ConnectionString, new AseRelationalConnection(CreateDependencies()).ConnectionString);

    /// <summary>
    ///     No se verifica acá que el pooling "funcione" de punta a punta (eso requiere una instancia real
    ///     de ASE, ver Fase 8) — solo que el connection string se pasa intacto al driver, que es quien
    ///     interpreta `Pooling`/`Max Pool Size`/`Min Pool Size` (ver DECISIONS.md, Fase 2).
    /// </summary>
    [Fact]
    public void Pooling_related_keywords_are_passed_through_untouched_to_the_driver()
    {
        using var connection = new AseRelationalConnection(CreateDependencies());

        Assert.Contains("Pooling=true", connection.DbConnection.ConnectionString);
        Assert.Contains("Max Pool Size=50", connection.DbConnection.ConnectionString);
    }

    private static RelationalConnectionDependencies CreateDependencies()
    {
        var options = new DbContextOptionsBuilder()
            .UseAse(ConnectionString)
            .Options;

        // RelationalConnection SIEMPRE pasa el connection string por ConnectionStringResolver
        // (no solo para el formato "Name=..."), así que el mock tiene que ser un passthrough —
        // un mock "loose" sin Setup devuelve null y rompe todo silenciosamente.
        var connectionStringResolver = new Mock<INamedConnectionStringResolver>();
        connectionStringResolver
            .Setup(r => r.ResolveConnectionString(It.IsAny<string>()))
            .Returns((string s) => s);

        return new RelationalConnectionDependencies(
            options,
            Mock.Of<IDiagnosticsLogger<DbLoggerCategory.Database.Transaction>>(),
            Mock.Of<IRelationalConnectionDiagnosticsLogger>(),
            connectionStringResolver.Object,
            Mock.Of<IRelationalTransactionFactory>(),
            Mock.Of<ICurrentDbContext>(),
            Mock.Of<IRelationalCommandBuilderFactory>());
    }
}
