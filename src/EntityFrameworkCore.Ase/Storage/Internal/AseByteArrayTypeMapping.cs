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
///     Igual motivo que <see cref="AseStringTypeMapping" />: el constructor público de
///     <see cref="ByteArrayTypeMapping" /> fuerza <see cref="StoreTypePostfix.None" />, necesitamos
///     <see cref="StoreTypePostfix.Size" /> para que `varbinary(50)` se genere con el tamaño.
/// </remarks>
public class AseByteArrayTypeMapping : ByteArrayTypeMapping
{
    public AseByteArrayTypeMapping(
        string storeType,
        DbType? dbType = System.Data.DbType.Binary,
        int? size = null,
        bool fixedLength = false,
        StoreTypePostfix storeTypePostfix = StoreTypePostfix.Size)
        : base(
            new RelationalTypeMappingParameters(
                new CoreTypeMappingParameters(typeof(byte[])),
                storeType,
                storeTypePostfix,
                dbType,
                unicode: false,
                size,
                fixedLength))
    {
    }

    protected AseByteArrayTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new AseByteArrayTypeMapping(parameters);
}
