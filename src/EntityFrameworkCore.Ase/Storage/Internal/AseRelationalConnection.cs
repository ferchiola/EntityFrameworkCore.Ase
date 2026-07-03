using System.Data.Common;
using System.Text;
using AdoNetCore.AseClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class AseRelationalConnection : RelationalConnection, IAseConnection
{
    static AseRelationalConnection()
    {
        // Descubierto probando contra una instancia real (ver DECISIONS.md, Fase 4): si el server
        // tiene configurado un charset "exótico" para .NET como cp850 (default bastante común en
        // instalaciones ASE en Windows), AdoNetCore.AseClient tira
        // "Server environment changed to unsupported charset 'cp850'" al loguear, salvo que el
        // proceso tenga registrado un EncodingProvider que sepa resolverlo. Se registra acá, una sola
        // vez por proceso, para que cualquier consumidor del provider no tenga que enterarse de este
        // detalle del driver.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public AseRelationalConnection(RelationalConnectionDependencies dependencies)
        : base(dependencies)
    {
    }

    /// <summary>
    ///     Crea la <see cref="DbConnection" /> concreta usando el driver <c>AdoNetCore.AseClient</c>.
    /// </summary>
    /// <remarks>
    ///     El connection string se pasa tal cual al driver (delegación, sin parseo propio) — soporta
    ///     `Pooling`, `Max Pool Size` y `Min Pool Size` de forma nativa (ver DECISIONS.md, Fase 2), así
    ///     que connection pooling "gratis" mientras no lo desactive el usuario en el connection string.
    /// </remarks>
    protected override DbConnection CreateDbConnection()
        => new AseConnection(GetValidatedConnectionString());

    // SupportsAmbientTransactions: se deja el default de RelationalConnection (false). ASE no tiene
    // un mecanismo de ambient transactions (System.Transactions) documentado ni soportado por
    // AdoNetCore.AseClient — no hay que asumir que funciona igual que SQL Server (ver DECISIONS.md).

    /// <remarks>
    ///     Confirmado contra ASE real (Fase 5): usa <c>master</c> como base administrativa, igual que
    ///     SQL Server. <c>AseConnectionStringBuilder</c> del driver es <see langword="internal" /> (no
    ///     usable desde acá) y no expone properties propias igual — se usa el
    ///     <see cref="System.Data.Common.DbConnectionStringBuilder" /> genérico para cambiar el
    ///     keyword <c>Database</c> del connection string.
    /// </remarks>
    public virtual IAseConnection CreateMasterConnection()
    {
        var connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = GetValidatedConnectionString() };
        connectionStringBuilder["Database"] = "master";

        var contextOptions = new DbContextOptionsBuilder()
            .UseAse(connectionStringBuilder.ConnectionString)
            .Options;

        return new AseRelationalConnection(Dependencies with { ContextOptions = contextOptions });
    }
}
