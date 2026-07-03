using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class AseTypeMappingSource : RelationalTypeMappingSource
{
    // bool/byte/short/int/long/DateTime ya tienen un ".Default" genérico de EF Core cuyo nombre de
    // store type coincide EXACTAMENTE con el tipo real de ASE (confirmado contra la tabla de tipos
    // soportados de AdoNetCore.AseClient, ver DECISIONS.md) — no hace falta ninguna clase propia para
    // esos. double/Guid/decimal/string/byte[]/DateTimeOffset/float sí necesitan mapeos propios.

    private static readonly DoubleTypeMapping DoublePrecision = new("double precision");

    /// <remarks>
    ///     <b>Bug real encontrado y corregido</b>: la Fase 3 original asumía que
    ///     <see cref="FloatTypeMapping" />.Default (StoreType <c>"float"</c>) era el mapeo correcto
    ///     para CLR <c>float</c> (System.Single), basándose en la tabla de tipos soportados del driver
    ///     sin verificar tamaños contra ASE real. Confirmado después contra la instancia real: una
    ///     columna ASE <c>float</c> (sin precisión) es de <b>8 bytes</b> y el driver la devuelve como
    ///     <c>System.Double</c> — <c>real</c> es el tipo de <b>4 bytes</b> que sí corresponde a
    ///     <c>System.Single</c>. Con el mapeo viejo, cualquier propiedad <c>float</c> generaba una
    ///     columna <c>float</c> (doble precisión real) declarada como si fuera de precisión simple —
    ///     no rompía en tiempo de ejecución (ASE/el driver hacen la conversión numérica sin
    ///     problema), pero era semánticamente incorrecto y confundía al scaffolding (una columna
    ///     <c>float</c> real se leía de vuelta como <c>double</c>, no <c>float</c>).
    /// </remarks>
    private static readonly FloatTypeMapping RealSinglePrecision = new("real");

    // Distinta instancia de DoublePrecision aunque mismo CLR type (double): así el scaffolding puede
    // distinguir si la columna real dice "float" o "double precision" y generar el HasColumnType
    // correcto en cada caso, en vez de mostrar siempre el mismo nombre de tipo.
    private static readonly DoubleTypeMapping FloatAsDouble = new("float");

    // ASE no tiene tipo GUID/UUID nativo. El driver acepta DbType.Guid y convierte a binary por debajo
    // ("technically ASE does not support GUID or UUID types. Our driver supports it, but converts to
    // Binary under the hood" — README de AdoNetCore.AseClient) — se declara la columna como binary(16).
    // Pendiente de verificar el round-trip contra la instancia real (ver Fase 8).
    private static readonly GuidTypeMapping GuidAsBinary = new("binary(16)", DbType.Guid);

    private static readonly AseDecimalTypeMapping DecimalDefault = new("decimal", precision: 18, scale: 2);

    // "smalldatetime" (4 bytes, precisión de minuto) es un tipo real de ASE distinto de "datetime" (8
    // bytes) — el driver lo devuelve igual como System.DateTime, pero necesita su propia instancia
    // con StoreType "smalldatetime" por el mismo motivo que "real" de arriba.
    private static readonly DateTimeTypeMapping SmallDateTime = new("smalldatetime");

    // "money" (8 bytes, precisión/escala fijas 19/4) — confirmado contra ASE real que el driver lo
    // devuelve como System.Decimal. A diferencia de decimal/numeric, ASE no acepta precisión/escala
    // entre paréntesis para money (es un tipo fijo), por eso StoreTypePostfix.None.
    private static readonly AseDecimalTypeMapping Money =
        new("money", precision: 19, scale: 4, storeTypePostfix: StoreTypePostfix.None);

    private static readonly AseDateTimeOffsetTypeMapping DateTimeOffsetAsDateTime = new();

    // ASE distingue tipos "ANSI" (varchar/char/text, charset del server) de tipos Unicode
    // (univarchar/unichar/unitext, UTF-16). Igual que el provider de SQL Server, por defecto mapeamos
    // string a Unicode (evita pérdida de datos con cualquier input) salvo que la propiedad tenga
    // IsUnicode(false) configurado explícitamente.
    private static readonly AseStringTypeMapping VariableLengthUnicodeString = new("univarchar", unicode: true);
    private static readonly AseStringTypeMapping FixedLengthUnicodeString = new("unichar", unicode: true, fixedLength: true);
    private static readonly AseStringTypeMapping UnboundedUnicodeString = new("unitext", unicode: true, storeTypePostfix: StoreTypePostfix.None);
    private static readonly AseStringTypeMapping VariableLengthAnsiString = new("varchar");
    private static readonly AseStringTypeMapping FixedLengthAnsiString = new("char", fixedLength: true);
    private static readonly AseStringTypeMapping UnboundedAnsiString = new("text", storeTypePostfix: StoreTypePostfix.None);

    private static readonly AseByteArrayTypeMapping VariableLengthBinary = new("varbinary");
    private static readonly AseByteArrayTypeMapping FixedLengthBinary = new("binary", fixedLength: true);
    private static readonly AseByteArrayTypeMapping UnboundedBinary = new("image", storeTypePostfix: StoreTypePostfix.None);

    // Longitud máxima de varchar/univarchar/varbinary/binary en ASE 16 antes de necesitar el tipo
    // "unbounded" (text/unitext/image). A confirmar contra la instancia real — depende de la
    // configuración del server (page size), este es el valor típico documentado por SAP.
    private const int MaxBoundedLength = 16384;

    private static readonly Dictionary<Type, RelationalTypeMapping> ClrTypeMappings = new()
    {
        { typeof(bool), BoolTypeMapping.Default },
        { typeof(byte), ByteTypeMapping.Default },
        { typeof(short), ShortTypeMapping.Default },
        { typeof(int), IntTypeMapping.Default },
        { typeof(long), LongTypeMapping.Default },
        { typeof(float), RealSinglePrecision },
        { typeof(double), DoublePrecision },
        { typeof(decimal), DecimalDefault },
        { typeof(DateTime), DateTimeTypeMapping.Default },
        { typeof(DateTimeOffset), DateTimeOffsetAsDateTime },
        { typeof(Guid), GuidAsBinary }
    };

    private static readonly Dictionary<string, RelationalTypeMapping> StoreTypeMappings =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "bit", BoolTypeMapping.Default },
            { "tinyint", ByteTypeMapping.Default },
            { "smallint", ShortTypeMapping.Default },
            { "int", IntTypeMapping.Default },
            { "bigint", LongTypeMapping.Default },
            { "real", RealSinglePrecision },
            { "float", FloatAsDouble },
            { "double precision", DoublePrecision },
            { "decimal", DecimalDefault },
            { "numeric", DecimalDefault },
            { "money", Money },
            { "datetime", DateTimeTypeMapping.Default },
            { "smalldatetime", SmallDateTime },
            { "binary", FixedLengthBinary },
            { "varbinary", VariableLengthBinary },
            { "image", UnboundedBinary },
            { "char", FixedLengthAnsiString },
            { "varchar", VariableLengthAnsiString },
            { "text", UnboundedAnsiString },
            { "unichar", FixedLengthUnicodeString },
            { "univarchar", VariableLengthUnicodeString },
            { "unitext", UnboundedUnicodeString }
        };

    public AseTypeMappingSource(
        TypeMappingSourceDependencies dependencies,
        RelationalTypeMappingSourceDependencies relationalDependencies)
        : base(dependencies, relationalDependencies)
    {
    }

    protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        => base.FindMapping(mappingInfo) ?? FindRawMapping(mappingInfo)?.WithTypeMappingInfo(mappingInfo);

    private static RelationalTypeMapping? FindRawMapping(RelationalTypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        var storeTypeName = mappingInfo.StoreTypeNameBase;

        // Se pidió un tipo de columna explícito (HasColumnType(...) o scaffolding) — priorizarlo.
        if (storeTypeName != null && StoreTypeMappings.TryGetValue(storeTypeName, out var mapping))
        {
            return clrType == null || mapping.ClrType == clrType ? mapping : null;
        }

        if (clrType == null)
        {
            return null;
        }

        if (clrType == typeof(string))
        {
            return FindStringMapping(mappingInfo);
        }

        if (clrType == typeof(byte[]))
        {
            return FindByteArrayMapping(mappingInfo);
        }

        return ClrTypeMappings.GetValueOrDefault(clrType);
    }

    private static RelationalTypeMapping FindStringMapping(RelationalTypeMappingInfo mappingInfo)
    {
        var isAnsi = mappingInfo.IsUnicode == false;
        var isFixedLength = mappingInfo.IsFixedLength == true;
        var size = mappingInfo.Size;

        if (size is null or < 0 or > MaxBoundedLength)
        {
            return isAnsi ? UnboundedAnsiString : UnboundedUnicodeString;
        }

        return new AseStringTypeMapping(
            isAnsi
                ? isFixedLength ? "char" : "varchar"
                : isFixedLength ? "unichar" : "univarchar",
            unicode: !isAnsi,
            size: size,
            fixedLength: isFixedLength);
    }

    private static RelationalTypeMapping FindByteArrayMapping(RelationalTypeMappingInfo mappingInfo)
    {
        var isFixedLength = mappingInfo.IsFixedLength == true;
        var size = mappingInfo.Size;

        if (size is null or < 0 or > MaxBoundedLength)
        {
            return UnboundedBinary;
        }

        return new AseByteArrayTypeMapping(
            isFixedLength ? "binary" : "varbinary",
            size: size,
            fixedLength: isFixedLength);
    }
}
