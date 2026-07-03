using EntityFrameworkCore.Ase.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace EntityFrameworkCore.Ase.Tests.Migrations;

// No hace falta una conexión real acá: construir el Model y generar el SQL de una migración son
// operaciones puramente de metadata/string, no tocan la base. La conexión de UseAse nunca se abre.
public class AseMigrationsSqlGeneratorTests
{
    private class LockSchemeContext : DbContext
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseAse("Data Source=unused;Port=5000;Database=unused;Uid=unused;Pwd=unused;");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Widget>().ForAseUseLockScheme(AseLockScheme.DataRows);
    }

    private class Widget
    {
        public int Id { get; set; }
    }

    private static CreateTableOperation GetWidgetsCreateTableOperation()
    {
        using var context = new LockSchemeContext();
        var differ = context.GetService<IMigrationsModelDiffer>();
        var operations = differ.GetDifferences(null, context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        return Assert.IsType<CreateTableOperation>(Assert.Single(operations));
    }

    [Fact]
    public void ForAseUseLockScheme_annotation_flows_from_model_to_CreateTableOperation()
    {
        var operation = GetWidgetsCreateTableOperation();

        Assert.Equal(AseLockScheme.DataRows, operation[AseAnnotationNames.LockScheme]);
    }

    [Fact]
    public void ForAseUseLockScheme_generates_LOCK_clause_in_CREATE_TABLE_sql()
    {
        using var context = new LockSchemeContext();
        var operation = GetWidgetsCreateTableOperation();

        var sqlGenerator = context.GetService<IMigrationsSqlGenerator>();
        var commands = sqlGenerator.Generate([operation], context.Model);

        var sql = Assert.Single(commands).CommandText;
        Assert.Contains("LOCK DATAROWS", sql);
    }
}
