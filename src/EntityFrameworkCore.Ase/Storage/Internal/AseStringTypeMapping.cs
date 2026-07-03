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
///     Existe solo porque el constructor público de <see cref="StringTypeMapping" /> fuerza
///     <see cref="StoreTypePostfix.None" /> — sin esto, `varchar`/`univarchar` nunca aparecerían con el
///     tamaño configurado (ej. `varchar(50)`) en el DDL generado.
/// </remarks>
public class AseStringTypeMapping : StringTypeMapping
{
    public AseStringTypeMapping(
        string storeType,
        DbType? dbType = null,
        bool unicode = false,
        int? size = null,
        bool fixedLength = false,
        StoreTypePostfix storeTypePostfix = StoreTypePostfix.Size)
        : base(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(string)),
                storeType,
                storeTypePostfix,
                dbType,
                unicode,
                size,
                fixedLength))
    {
    }

    protected AseStringTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AseStringTypeMapping(parameters);
}
