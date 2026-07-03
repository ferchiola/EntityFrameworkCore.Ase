using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.Ase.Update.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Usa <see cref="SingularModificationCommandBatch" /> (un comando por round-trip, sin batching) —
///     no se verificó si <c>AdoNetCore.AseClient</c>/ASE soportan agrupar varios INSERT/UPDATE/DELETE
///     en un solo batch, así que se eligió la opción conservadora en vez de asumir que sí (mismo
///     criterio que SQLite, ver <c>SqliteModificationCommandBatchFactory</c>). Se puede optimizar más
///     adelante si se confirma soporte de batching.
/// </remarks>
public class AseModificationCommandBatchFactory : IModificationCommandBatchFactory
{
    private readonly ModificationCommandBatchFactoryDependencies _dependencies;

    public AseModificationCommandBatchFactory(ModificationCommandBatchFactoryDependencies dependencies)
        => _dependencies = dependencies;

    public virtual ModificationCommandBatch Create()
        => new SingularModificationCommandBatch(_dependencies);
}
