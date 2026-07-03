using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     <para>
///         SAP ASE no tiene un tipo equivalente a <c>datetimeoffset</c> de SQL Server, y el driver
///         <c>AdoNetCore.AseClient</c> lo confirma explícitamente ("ASE does not support a DateTimeOffset
///         type. Use DateTime instead").
///     </para>
///     <para>
///         <b>Limitación conocida (ver DECISIONS.md, Fase 3):</b> se convierte a <see cref="DateTime" />
///         en UTC para guardar, y se reconstruye como <see cref="DateTimeOffset" /> con offset cero al
///         leer. Esto preserva el instante exacto en el tiempo pero <b>pierde el offset original</b>
///         (ej. guardar <c>2026-01-01T10:00:00+05:00</c> y leer <c>2026-01-01T05:00:00+00:00</c> — el
///         mismo instante, offset distinto). Si un proyecto necesita preservar el offset original tal
///         cual, tiene que modelarlo como columnas separadas (DateTime + offset), no con este mapeo.
///     </para>
/// </remarks>
public class AseDateTimeOffsetTypeMapping : RelationalTypeMapping
{
    private static readonly ValueConverter<DateTimeOffset, DateTime> UtcConverter = new(
        d => d.UtcDateTime,
        d => new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)));

    public AseDateTimeOffsetTypeMapping(string storeType = "datetime")
        : base(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(DateTimeOffset), converter: UtcConverter),
                storeType,
                dbType: System.Data.DbType.DateTime))
    {
    }

    protected AseDateTimeOffsetTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AseDateTimeOffsetTypeMapping(parameters);
}
