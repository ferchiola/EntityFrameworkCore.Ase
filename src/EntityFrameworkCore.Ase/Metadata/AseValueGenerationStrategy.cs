namespace EntityFrameworkCore.Ase.Metadata;

/// <summary>
///     Estrategias de generación de valores propias de Ase.
/// </summary>
/// <remarks>
///     Solo <see cref="IdentityColumn" /> por ahora — ASE no tiene secuencias del estilo
///     PostgreSQL/SQL Server modernas verificadas todavía (no investigado, ver DECISIONS.md Fase 5).
/// </remarks>
public enum AseValueGenerationStrategy
{
    /// <summary>El valor no lo genera la base — lo provee la aplicación.</summary>
    None,

    /// <summary>Columna <c>IDENTITY</c> (autoincremental, generada por ASE al insertar).</summary>
    IdentityColumn
}
