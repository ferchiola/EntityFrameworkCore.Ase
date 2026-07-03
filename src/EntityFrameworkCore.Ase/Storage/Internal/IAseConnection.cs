using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Service lifetime: Scoped — cada <see cref="Microsoft.EntityFrameworkCore.DbContext" /> usa su propia
///     instancia (igual que <c>ISqlServerConnection</c> en el provider oficial).
/// </remarks>
public interface IAseConnection : IRelationalConnection
{
    /// <summary>
    ///     Crea una conexión a la base administrativa (<c>master</c>, confirmado que ASE la usa igual
    ///     que SQL Server — ver DECISIONS.md, Fase 5) para operaciones de creación/borrado de base.
    /// </summary>
    IAseConnection CreateMasterConnection();
}
