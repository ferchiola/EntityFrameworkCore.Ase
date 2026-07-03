using EntityFrameworkCore.Ase.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityFrameworkCore.Ase.Tests.Infrastructure;

public class UseAseTests
{
    private const string DummyConnectionString = "Server=localhost;Database=Dummy;Uid=sa;Pwd=dummy;";

    [Fact]
    public void UseAse_registers_AseOptionsExtension_with_the_given_connection_string()
    {
        var optionsBuilder = new DbContextOptionsBuilder();

        optionsBuilder.UseAse(DummyConnectionString);

        var extension = optionsBuilder.Options.FindExtension<AseOptionsExtension>();

        Assert.NotNull(extension);
        Assert.Equal(DummyConnectionString, extension.ConnectionString);
    }

    [Fact]
    public void UseAse_generic_overload_returns_the_typed_builder_for_chaining()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DummyContext>();

        var result = optionsBuilder.UseAse(DummyConnectionString);

        Assert.Same(optionsBuilder, result);
    }

    [Fact]
    public void AddEntityFrameworkAse_registers_the_Ase_database_provider()
    {
        var services = new ServiceCollection();

        services.AddEntityFrameworkAse();

        using var serviceProvider = services.BuildServiceProvider();
        var providers = serviceProvider.GetServices<IDatabaseProvider>();

        Assert.Contains(providers, p => p.Name == typeof(AseOptionsExtension).Assembly.GetName().Name);
    }

    private class DummyContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseAse(DummyConnectionString);
    }
}
