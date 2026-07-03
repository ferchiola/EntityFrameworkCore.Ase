using EntityFrameworkCore.Ase.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Permite configuración específica de Ase sobre <see cref="DbContextOptions" />.
/// </summary>
/// <remarks>
///     Las instancias de esta clase se obtienen a través de <see cref="AseDbContextOptionsExtensions.UseAse(DbContextOptionsBuilder, string?, System.Action{AseDbContextOptionsBuilder}?)" />
///     — no está pensada para construirse directamente.
///
///     Vacía por ahora: todavía no hay ninguna opción específica de Ase que valga la pena exponer acá
///     (candidatas para fases siguientes: nivel de compatibilidad de ASE, estrategia de paginación,
///     comportamiento de identity — ver Fase 0/CLAUDE.md).
/// </remarks>
public class AseDbContextOptionsBuilder
    : RelationalDbContextOptionsBuilder<AseDbContextOptionsBuilder, AseOptionsExtension>
{
    public AseDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        : base(optionsBuilder)
    {
    }
}
