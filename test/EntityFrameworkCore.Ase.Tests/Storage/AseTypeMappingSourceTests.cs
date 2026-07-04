using EntityFrameworkCore.Ase.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Ase.Tests.Storage;

public class AseTypeMappingSourceTests
{
    // No se usa un DbContext real acá a propósito: construir un DbContext.Model completo dispara la
    // resolución de servicios que todavía no existen (ISqlGenerationHelper es de la Fase 4). El
    // overload público FindMapping(Type, storeTypeName, unicode, size, ...) de IRelationalTypeMappingSource
    // permite testear la lógica de mapeo sin necesitar ningún Property/Model real.
    private static IRelationalTypeMappingSource CreateTypeMappingSource()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkAse();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IRelationalTypeMappingSource>();
    }

    [Theory]
    [InlineData(typeof(bool), "bit")]
    [InlineData(typeof(byte), "tinyint")]
    [InlineData(typeof(short), "smallint")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(long), "bigint")]
    [InlineData(typeof(float), "real")]
    [InlineData(typeof(double), "double precision")]
    [InlineData(typeof(decimal), "decimal(18,2)")]
    [InlineData(typeof(DateTime), "datetime")]
    [InlineData(typeof(DateTimeOffset), "datetime")]
    [InlineData(typeof(Guid), "binary(16)")]
    public void FindMapping_maps_simple_CLR_types_to_the_expected_Ase_store_type(Type clrType, string expectedStoreType)
    {
        var mapping = CreateTypeMappingSource().FindMapping(clrType);

        Assert.NotNull(mapping);
        Assert.Equal(expectedStoreType, mapping.StoreType);
    }

    [Fact]
    public void DateTimeOffset_mapping_converts_to_UTC_DateTime_for_storage()
    {
        var mapping = CreateTypeMappingSource().FindMapping(typeof(DateTimeOffset));
        var converter = mapping!.Converter;

        Assert.NotNull(converter);

        var original = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(5));
        var stored = (DateTime)converter.ConvertToProvider(original)!;

        // Se pierde el offset original (+05:00) pero se preserva el instante (convertido a UTC).
        Assert.Equal(DateTimeKind.Utc, stored.Kind);
        Assert.Equal(original.UtcDateTime, stored);

        var roundTripped = (DateTimeOffset)converter.ConvertFromProvider(stored)!;
        Assert.Equal(TimeSpan.Zero, roundTripped.Offset);
        Assert.Equal(original.ToUniversalTime(), roundTripped);
    }

    [Fact]
    public void Guid_mapping_uses_DbType_Guid_so_the_driver_handles_the_binary_conversion()
    {
        var mapping = CreateTypeMappingSource().FindMapping(typeof(Guid));

        Assert.Equal(System.Data.DbType.Guid, mapping!.DbType);
    }

    [Theory]
    [InlineData(true, 50, "univarchar(50)")] // unicode, con longitud
    [InlineData(false, 50, "varchar(50)")] // ansi, con longitud
    [InlineData(true, null, "unitext")] // unicode, sin longitud (unbounded)
    [InlineData(false, null, "text")] // ansi, sin longitud (unbounded)
    public void String_mapping_picks_bounded_vs_unbounded_and_unicode_vs_ansi_store_type(
        bool unicode, int? size, string expectedStoreType)
    {
        var mapping = CreateTypeMappingSource().FindMapping(typeof(string), storeTypeName: null, unicode: unicode, size: size);

        Assert.Equal(expectedStoreType, mapping!.StoreType);
    }

    [Theory]
    [InlineData(16, "varbinary(16)")]
    [InlineData(null, "image")]
    public void Byte_array_mapping_picks_bounded_vs_unbounded_store_type(int? size, string expectedStoreType)
    {
        var mapping = CreateTypeMappingSource().FindMapping(typeof(byte[]), storeTypeName: null, size: size);

        Assert.Equal(expectedStoreType, mapping!.StoreType);
    }

    [Theory]
    [InlineData("bit", typeof(bool))]
    [InlineData("varchar", typeof(string))]
    [InlineData("univarchar", typeof(string))]
    [InlineData("varbinary", typeof(byte[]))]
    [InlineData("real", typeof(float))]
    [InlineData("smalldatetime", typeof(DateTime))]
    [InlineData("money", typeof(decimal))]
    [InlineData("smallmoney", typeof(decimal))]
    [InlineData("date", typeof(DateTime))]
    [InlineData("time", typeof(DateTime))]
    public void FindMapping_by_explicit_store_type_name_resolves_the_right_CLR_type(string storeTypeName, Type expectedClrType)
    {
        var mapping = CreateTypeMappingSource().FindMapping(storeTypeName);

        Assert.NotNull(mapping);
        Assert.Equal(expectedClrType, mapping.ClrType);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("smallmoney")]
    public void Money_mappings_have_no_precision_scale_suffix_in_the_store_type(string storeTypeName)
    {
        // A diferencia de decimal/numeric, ASE no acepta "money(19,4)"/"smallmoney(10,4)" — la
        // precisión/escala de money/smallmoney es fija e implícita (ver DECISIONS.md).
        var mapping = CreateTypeMappingSource().FindMapping(storeTypeName);

        Assert.Equal(storeTypeName, mapping!.StoreType);
    }

    [Fact]
    public void Real_and_float_store_types_resolve_to_different_CLR_precision()
    {
        // "real" (4 bytes) -> System.Single, "float" (8 bytes) -> System.Double — confirmado contra
        // ASE real que son tipos distintos, no sinónimos (ver DECISIONS.md).
        var typeMappingSource = CreateTypeMappingSource();

        Assert.Equal(typeof(float), typeMappingSource.FindMapping("real")!.ClrType);
        Assert.Equal(typeof(double), typeMappingSource.FindMapping("float")!.ClrType);
    }
}
