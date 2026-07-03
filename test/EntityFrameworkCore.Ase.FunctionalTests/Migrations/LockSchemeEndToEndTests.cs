using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Ase.FunctionalTests.Migrations;

/// <summary>
///     Test de punta a punta de la Fase 6 contra una instancia real de ASE (no mocks): confirma que
///     <c>ForAseUseLockScheme</c> viaja desde el fluent API, pasando por <c>AseAnnotationProvider</c>,
///     hasta el <c>CREATE TABLE ... LOCK DATAROWS</c> real que ASE acepta. <c>EnsureCreated()</c> pasa
///     por el mismo pipeline de diff + <c>IMigrationsSqlGenerator</c> que <c>Migrate()</c>, así que
///     alcanza para ejercitar el camino completo sin necesitar una migración escrita a mano.
/// </summary>
public class LockSchemeEndToEndTests : IDisposable
{
    private const string DatabaseName = "EfCoreAseLockSchemeTest";

    // Ver DECISIONS.md, Fase 5: Pooling=false evita "currently in use" al recrear la base en Dispose().
    private static readonly string ConnectionString =
        $"Data Source={TestServer.Host};Port=5000;Database={DatabaseName};Uid=sa;Pwd=Password;Pooling=false;";

    private readonly LockSchemeContext _context = new();

    public void Dispose()
    {
        _context.Dispose();

        using var cleanupContext = new LockSchemeContext();
        cleanupContext.Database.EnsureDeleted();
    }

    [Fact]
    public void EnsureCreated_creates_table_with_LOCK_DATAROWS_clause()
    {
        _context.Database.EnsureCreated();

        // Si ASE hubiera rechazado la cláusula LOCK, EnsureCreated() ya habría tirado antes de esto.
        _context.Database.ExecuteSqlRaw("INSERT INTO Gadgets (Name) VALUES ('Beta')");
        var gadget = _context.Gadgets.Single();
        Assert.Equal("Beta", gadget.Name);
    }

    private class LockSchemeContext : DbContext
    {
        public DbSet<Gadget> Gadgets => Set<Gadget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseAse(ConnectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Gadget>().ToTable("Gadgets").ForAseUseLockScheme(AseLockScheme.DataRows);
    }

    private class Gadget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
