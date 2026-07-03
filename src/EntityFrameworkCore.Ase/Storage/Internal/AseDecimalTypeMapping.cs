using System.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.Ase.Storage.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Igual motivo que <see cref="AseStringTypeMapping" />: ni el constructor público de
///     <see cref="DecimalTypeMapping" /> ni el de <see cref="RelationalTypeMapping" /> del que depende
///     ponen <see cref="StoreTypePostfix.PrecisionAndScale" /> — sin esto, `decimal(18,2)` nunca
///     aparecería con precisión/escala en el DDL generado.
///     <c>storeTypePostfix</c> es <see cref="StoreTypePostfix.None" /> para <c>money</c>: a
///     diferencia de <c>decimal</c>/<c>numeric</c>, ASE no acepta (ni necesita) precisión/escala
///     entre paréntesis para <c>money</c> — confirmado contra ASE real que la precisión (19) y
///     escala (4) son fijas e implícitas en el tipo.
/// </remarks>
public class AseDecimalTypeMapping : DecimalTypeMapping
{
    public AseDecimalTypeMapping(
        string storeType,
        DbType? dbType = System.Data.DbType.Decimal,
        int? precision = null,
        int? scale = null,
        StoreTypePostfix storeTypePostfix = StoreTypePostfix.PrecisionAndScale)
        : base(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(decimal)),
                storeType,
                storeTypePostfix,
                dbType,
                precision: precision,
                scale: scale))
    {
    }

    protected AseDecimalTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AseDecimalTypeMapping(parameters);
}
