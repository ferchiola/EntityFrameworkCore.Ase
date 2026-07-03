using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace EntityFrameworkCore.Ase.FunctionalTests.Migrations;

/// <summary>
///     Test de punta a punta de la Fase 5 contra una instancia real de ASE (no mocks): crea una base
///     nueva, aplica una migración real (<c>Database.Migrate()</c>, no <c>EnsureCreated</c>) que
///     crea una tabla con columna <c>IDENTITY</c>, confirma que el historial de migraciones quedó
///     registrado, y borra todo al final — ejercita <c>AseDatabaseCreator</c>,
///     <c>AseMigrationsSqlGenerator</c> y <c>AseHistoryRepository</c> juntos.
/// </summary>
public class MigrationEndToEndTests : IDisposable
{
    private const string DatabaseName = "EfCoreAseMigrationTest";

    // Pooling=false es necesario acá: AdoNetCore.AseClient 0.19.2 no libera de forma confiable la
    // conexión física pooleada al cerrar/disponer el DbContext, así que un DROP DATABASE inmediato
    // después (en Dispose()) falla con "currently in use" incluso llamando AseConnection.ClearPools()
    // y reintentando varios segundos (confirmado contra ASE real, ver DECISIONS.md Fase 5). Con
    // pooling desactivado, Close() cierra la conexión de verdad y el DROP funciona al primer intento.
    // Esto solo importa en escenarios de crear/borrar la base repetidamente (tests); una app normal
    // que no dropea su propia base no lo necesita.
    private static readonly string ConnectionString =
        $"Data Source={TestServer.Host};Port=5000;Database={DatabaseName};Uid=sa;Pwd=Password;Pooling=false;";

    private readonly MigrationTestContext _context = new();

    public void Dispose()
    {
        // Cerrar el contexto ANTES de dropear la base: si la conexión de _context sigue "viva"
        // (aunque .NET la vea cerrada, el driver la puede seguir teniendo pooleada), DROP DATABASE
        // falla con "currently in use" — ver AseDatabaseCreator.Delete() y DECISIONS.md, Fase 5.
        _context.Dispose();

        using var cleanupContext = new MigrationTestContext();
        cleanupContext.Database.EnsureDeleted();
    }

    [Fact]
    public void Migrate_creates_database_table_with_identity_and_history_row()
    {
        // La base todavía no existe -> Migrate() tiene que crearla (AseDatabaseCreator.Create/Exists)
        // antes de poder aplicar la migración.
        _context.Database.Migrate();

        // La migración se aplicó: la tabla existe y el IDENTITY funciona sin especificar Id.
        _context.Database.ExecuteSqlRaw("INSERT INTO MigrationWidgets (Name) VALUES ('Alpha')");
        var widget = _context.MigrationWidgets.Single();
        Assert.True(widget.Id > 0);
        Assert.Equal("Alpha", widget.Name);

        // El historial de migraciones quedó registrado (AseHistoryRepository).
        var appliedMigrations = _context.Database.GetAppliedMigrations().ToList();
        Assert.Contains("20260101000000_Initial", appliedMigrations);
    }

    private class MigrationTestContext : DbContext
    {
        public DbSet<MigrationWidget> MigrationWidgets => Set<MigrationWidget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseAse(ConnectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MigrationWidget>().ToTable("MigrationWidgets");
    }

    private class MigrationWidget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // El atributo [DbContext] es obligatorio: MigrationsAssembly.Migrations filtra por
    // t.GetCustomAttribute<DbContextAttribute>()?.ContextType == contextType — sin él, la migración
    // se descarta en silencio (no aparece ni en GetMigrations() ni en ningún otro lado), confirmado
    // leyendo el código fuente real de MigrationsAssembly en dotnet/efcore (ver DECISIONS.md, Fase 5).
    [DbContextAttribute(typeof(MigrationTestContext))]
    [Migration("20260101000000_Initial")]
    private class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MigrationWidgets",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation(AseAnnotationNames.ValueGenerationStrategy, AseValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(50)", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_MigrationWidgets", x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "MigrationWidgets");
    }
}
