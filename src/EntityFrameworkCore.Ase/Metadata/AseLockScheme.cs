namespace EntityFrameworkCore.Ase.Metadata;

/// <summary>
///     Esquema de locking de una tabla ASE (cláusula <c>LOCK</c> de <c>CREATE TABLE</c>).
/// </summary>
/// <remarks>
///     Sintaxis confirmada contra ASE 16.1.0 real (ver DECISIONS.md, Fase 6):
///     <c>CREATE TABLE t (...) LOCK DATAROWS</c> (y <c>ALLPAGES</c>/<c>DATAPAGES</c>) funcionan sin
///     errores. No tiene equivalente en SQL Server — es la primera feature genuinamente propia de ASE
///     que se expone por fluent API en este provider.
/// </remarks>
public enum AseLockScheme
{
    /// <summary>Locking a nivel de página de datos e índice — el esquema clásico/legacy de Sybase.</summary>
    AllPages,

    /// <summary>Locking a nivel de página de datos únicamente.</summary>
    DataPages,

    /// <summary>Locking a nivel de fila — mayor concurrencia, default en instalaciones modernas de ASE.</summary>
    DataRows
}
