using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.Ase.Update.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Placeholder: <see cref="UpdateSqlGenerator" /> no tiene miembros abstractos (los defaults ANSI
///     ya generan INSERT/UPDATE/DELETE básicos), así que esto compila y probablemente funcione para
///     casos simples, pero <b>no se probó SaveChanges contra ASE real todavía</b> — eso es una fase
///     posterior (no pedida explícitamente en la Fase 4, que es solo sobre lectura). Se agregó acá
///     únicamente porque hace falta un <see cref="IUpdateSqlGenerator" /> registrado para que el
///     árbol de dependencias de <see cref="Microsoft.EntityFrameworkCore.DbContext" /> se resuelva
///     (hasta para ejecutar queries de lectura EF Core arma todo el árbol de servicios, incluido el de
///     escritura) — ver DECISIONS.md, Fase 4.
/// </remarks>
public class AseUpdateSqlGenerator : UpdateSqlGenerator
{
    public AseUpdateSqlGenerator(UpdateSqlGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }
}
