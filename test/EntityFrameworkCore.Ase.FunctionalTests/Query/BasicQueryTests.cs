using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Ase.FunctionalTests.Query;

/// <summary>
///     Tests de punta a punta contra una instancia real de SAP ASE (no mocks) — validan que el
///     pipeline completo armado en las Fases 1-4 funciona con LINQ real, no solo que compile.
/// </summary>
/// <remarks>
///     <para>
///         Connection string apunta a la instancia de desarrollo real usada durante la Fase 4 (ver
///         DECISIONS.md). Estos tests requieren esa instancia disponible — no son parte del build
///         normal de CI todavía (no hay CI configurado, ver Fase 0), se corren manualmente.
///     </para>
///     <para>
///         Los datos se siembran con SQL crudo (<see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw" />),
///         no con <c>SaveChanges()</c> — el pipeline de INSERT/UPDATE (<c>IUpdateSqlGenerator</c>) es
///         de una fase posterior, todavía no existe. Fase 4 es específicamente sobre generación de SQL
///         para lecturas (LINQ), no sobre <c>SaveChanges</c>.
///     </para>
/// </remarks>
public class BasicQueryTests : IDisposable
{
    private static readonly string ConnectionString = $"Data Source={TestServer.Host};Port=5000;Database=master;Uid=sa;Pwd=Password;";

    private readonly WidgetContext _context;

    public BasicQueryTests()
    {
        _context = new WidgetContext();
        _context.Database.ExecuteSqlRaw("IF OBJECT_ID('AseProviderTestWidgets') IS NOT NULL DROP TABLE AseProviderTestWidgets");
        _context.Database.ExecuteSqlRaw(
            "CREATE TABLE AseProviderTestWidgets (Id int PRIMARY KEY, Name varchar(50) NOT NULL, Price decimal(18,2) NOT NULL)");
        _context.Database.ExecuteSqlRaw(@"
            INSERT INTO AseProviderTestWidgets VALUES (1, 'Alpha', 10)
            INSERT INTO AseProviderTestWidgets VALUES (2, 'Beta', 20)
            INSERT INTO AseProviderTestWidgets VALUES (3, 'Gamma', 30)
            INSERT INTO AseProviderTestWidgets VALUES (4, 'Delta', 40)
            INSERT INTO AseProviderTestWidgets VALUES (5, 'Epsilon', 50)");
    }

    public void Dispose()
    {
        _context.Database.ExecuteSqlRaw("IF OBJECT_ID('AseProviderTestWidgets') IS NOT NULL DROP TABLE AseProviderTestWidgets");
        _context.Database.ExecuteSqlRaw("IF OBJECT_ID('AseProviderTestTags') IS NOT NULL DROP TABLE AseProviderTestTags");
        _context.Dispose();
    }

    [Fact]
    public void Where_and_OrderBy()
    {
        var results = _context.Widgets
            .Where(w => w.Price > 15m)
            .OrderByDescending(w => w.Price)
            .Select(w => w.Name)
            .ToList();

        Assert.Equal(["Epsilon", "Delta", "Gamma", "Beta"], results);
    }

    [Fact]
    public void Take_only_uses_TOP()
    {
        var results = _context.Widgets.OrderBy(w => w.Price).Take(2).Select(w => w.Name).ToList();

        Assert.Equal(["Alpha", "Beta"], results);
    }

    [Fact]
    public void Skip_and_Take_uses_ROWS_OFFSET_LIMIT()
    {
        var results = _context.Widgets.OrderBy(w => w.Price).Skip(1).Take(2).Select(w => w.Name).ToList();

        Assert.Equal(["Beta", "Gamma"], results);
    }

    [Fact]
    public void Skip_only()
    {
        var results = _context.Widgets.OrderBy(w => w.Price).Skip(3).Select(w => w.Name).ToList();

        Assert.Equal(["Delta", "Epsilon"], results);
    }

    [Fact]
    public void Contains_translates_to_LIKE()
    {
        var results = _context.Widgets.Where(w => w.Name.Contains("mm")).Select(w => w.Name).ToList();

        Assert.Equal(["Gamma"], results);
    }

    [Fact]
    public void StartsWith_translates_to_LIKE()
    {
        var results = _context.Widgets.Where(w => w.Name.StartsWith("Al")).Select(w => w.Name).ToList();

        Assert.Equal(["Alpha"], results);
    }

    [Fact]
    public void ToUpper_and_Length()
    {
        var result = _context.Widgets
            .Where(w => w.Name == "Alpha")
            .Select(w => new { Upper = w.Name.ToUpper(), Len = w.Name.Length })
            .Single();

        Assert.Equal("ALPHA", result.Upper);
        Assert.Equal(5, result.Len);
    }

    [Fact]
    public void Simple_join()
    {
        _context.Database.ExecuteSqlRaw("IF OBJECT_ID('AseProviderTestTags') IS NOT NULL DROP TABLE AseProviderTestTags");
        _context.Database.ExecuteSqlRaw("CREATE TABLE AseProviderTestTags (WidgetId int, Label varchar(20))");
        _context.Database.ExecuteSqlRaw("INSERT INTO AseProviderTestTags VALUES (1, 'shiny')");

        var query =
            from w in _context.Widgets
            join t in _context.Set<Tag>() on w.Id equals t.WidgetId
            select new { w.Name, t.Label };

        var result = query.Single();
        Assert.Equal("Alpha", result.Name);
        Assert.Equal("shiny", result.Label);
    }

    private class WidgetContext : DbContext
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseAse(ConnectionString);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>().ToTable("AseProviderTestWidgets");
            modelBuilder.Entity<Tag>().HasNoKey().ToTable("AseProviderTestTags");
        }
    }

    private class Widget
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    private class Tag
    {
        public int WidgetId { get; set; }
        public string Label { get; set; } = "";
    }
}
