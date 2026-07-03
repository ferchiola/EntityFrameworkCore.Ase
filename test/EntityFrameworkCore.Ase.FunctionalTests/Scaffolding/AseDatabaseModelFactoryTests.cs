using System.Text;
using AdoNetCore.AseClient;
using EntityFrameworkCore.Ase.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Xunit;

namespace EntityFrameworkCore.Ase.FunctionalTests.Scaffolding;

/// <summary>
///     Test de integración real (no mocks) de la feature de scaffolding armada antes de la Fase 7:
///     crea un esquema real (dos tablas, FK, dos índices) directamente por SQL crudo — sin pasar por
///     este provider en absoluto — y confirma que <see cref="AseDatabaseModelFactory" /> lo lee de
///     vuelta correctamente. Esto ejercita el mismo código que corre <c>dotnet ef dbcontext
///     scaffold</c> (se probó también manualmente con la CLI real de <c>dotnet-ef</c> antes de escribir
///     este test, ver DECISIONS.md).
/// </summary>
public class AseDatabaseModelFactoryTests : IDisposable
{
    static AseDatabaseModelFactoryTests()
    {
        // Este test abre conexiones AseConnection crudas antes de tocar AseDatabaseModelFactory (cuyo
        // constructor estático registra esto) — sin esto, la conexión de setup del propio test falla
        // con "Server environment changed to unsupported charset 'cp850'" (ver DECISIONS.md, Fase 4).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private const string DatabaseName = "EfCoreAseScaffoldTest";

    private static readonly string MasterConnectionString =
        $"Data Source={TestServer.Host};Port=5000;Database=master;Uid=sa;Pwd=Password;Pooling=false;";

    private static readonly string ConnectionString =
        $"Data Source={TestServer.Host};Port=5000;Database={DatabaseName};Uid=sa;Pwd=Password;Pooling=false;";

    public AseDatabaseModelFactoryTests()
    {
        using var master = new AseConnection(MasterConnectionString);
        master.Open();
        Exec(master, $"IF DB_ID('{DatabaseName}') IS NOT NULL DROP DATABASE {DatabaseName}");
        Exec(master, $"CREATE DATABASE {DatabaseName}");
        Exec(master, $"EXEC sp_dboption '{DatabaseName}', 'ddl in tran', true");

        using var connection = new AseConnection(ConnectionString);
        connection.Open();
        Exec(connection, """
            CREATE TABLE Authors (
                Id int IDENTITY,
                Name varchar(100) NOT NULL,
                Rating decimal(5,2) NULL,
                CONSTRAINT PK_Authors PRIMARY KEY (Id)
            )
            """);
        Exec(connection, """
            CREATE TABLE Books (
                Id int IDENTITY,
                Title varchar(200) NOT NULL,
                AuthorId int NOT NULL,
                CopiesSold bigint NULL,
                CONSTRAINT PK_Books PRIMARY KEY (Id),
                CONSTRAINT FK_Books_Authors FOREIGN KEY (AuthorId) REFERENCES Authors(Id)
            )
            """);
        Exec(connection, "CREATE UNIQUE INDEX IX_Books_Title ON Books(Title)");
        Exec(connection, "CREATE INDEX IX_Books_AuthorId ON Books(AuthorId)");
    }

    public void Dispose()
    {
        using var master = new AseConnection(MasterConnectionString);
        master.Open();
        Exec(master, $"DROP DATABASE {DatabaseName}");
    }

    [Fact]
    public void Create_reads_tables_columns_primary_keys_foreign_keys_and_indexes()
    {
        var factory = new AseDatabaseModelFactory();
        var model = factory.Create(ConnectionString, new DatabaseModelFactoryOptions());

        Assert.Equal(2, model.Tables.Count);

        var authors = Assert.Single(model.Tables, t => t.Name == "Authors");
        var idColumn = Assert.Single(authors.Columns, c => c.Name == "Id");
        Assert.Equal(ValueGenerated.OnAdd, idColumn.ValueGenerated);
        var nameColumn = Assert.Single(authors.Columns, c => c.Name == "Name");
        Assert.Equal("varchar(100)", nameColumn.StoreType);
        Assert.False(nameColumn.IsNullable);
        var ratingColumn = Assert.Single(authors.Columns, c => c.Name == "Rating");
        Assert.Equal("decimal(5,2)", ratingColumn.StoreType);
        Assert.True(ratingColumn.IsNullable);
        Assert.NotNull(authors.PrimaryKey);
        Assert.Equal("Id", Assert.Single(authors.PrimaryKey.Columns).Name);

        var books = Assert.Single(model.Tables, t => t.Name == "Books");
        var copiesSoldColumn = Assert.Single(books.Columns, c => c.Name == "CopiesSold");
        Assert.Equal("bigint", copiesSoldColumn.StoreType);

        // El índice que respalda la PK de Books no debe listarse de nuevo en Indexes.
        Assert.DoesNotContain(books.Indexes, i => i.Columns.Select(c => c.Name).SequenceEqual(["Id"]));

        var uniqueIndex = Assert.Single(books.Indexes, i => i.Name == "IX_Books_Title");
        Assert.True(uniqueIndex.IsUnique);
        Assert.Equal("Title", Assert.Single(uniqueIndex.Columns).Name);

        var nonUniqueIndex = Assert.Single(books.Indexes, i => i.Name == "IX_Books_AuthorId");
        Assert.False(nonUniqueIndex.IsUnique);

        var foreignKey = Assert.Single(books.ForeignKeys);
        Assert.Same(authors, foreignKey.PrincipalTable);
        Assert.Equal("AuthorId", Assert.Single(foreignKey.Columns).Name);
        Assert.Equal("Id", Assert.Single(foreignKey.PrincipalColumns).Name);
    }

    /// <remarks>
    ///     Reproduce el caso real encontrado scaffoldeando las bases de ejemplo de Sybase
    ///     (<c>pubs2</c>/<c>pubs3</c>), que usan tipos definidos por el usuario vía <c>sp_addtype</c>
    ///     (ej. <c>id</c> como alias de <c>varchar(11)</c>) — antes del fix, el scaffolding no podía
    ///     mapear esas columnas ("Could not find type mapping ... with data type 'id'"). Ver
    ///     DECISIONS.md.
    /// </remarks>
    [Fact]
    public void Create_resolves_user_defined_types_to_their_base_type()
    {
        using (var connection = new AseConnection(ConnectionString))
        {
            connection.Open();
            Exec(connection, "EXEC sp_addtype UdtId, 'varchar(11)'");
            Exec(connection, "CREATE TABLE Reviewers (Id UdtId NOT NULL, CONSTRAINT PK_Reviewers PRIMARY KEY (Id))");
        }

        var factory = new AseDatabaseModelFactory();
        var model = factory.Create(ConnectionString, new DatabaseModelFactoryOptions());

        var reviewers = Assert.Single(model.Tables, t => t.Name == "Reviewers");
        var idColumn = Assert.Single(reviewers.Columns, c => c.Name == "Id");
        Assert.Equal("varchar(11)", idColumn.StoreType);
    }

    [Fact]
    public void Create_respects_the_Tables_filter_and_drops_foreign_keys_to_excluded_tables()
    {
        var factory = new AseDatabaseModelFactory();
        var model = factory.Create(ConnectionString, new DatabaseModelFactoryOptions(tables: ["Books"], schemas: []));

        var books = Assert.Single(model.Tables);
        Assert.Equal("Books", books.Name);
        Assert.Empty(books.ForeignKeys);
    }

    private static void Exec(AseConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
