using System.Collections.Generic;
using EntityFrameworkCore.Ase.Diagnostics.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Ase.Infrastructure.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class AseOptionsExtension : RelationalOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    public AseOptionsExtension()
    {
    }

    // NB: When adding new options, make sure to update the copy ctor below.
    protected AseOptionsExtension(AseOptionsExtension copyFrom)
        : base(copyFrom)
    {
        // Nada propio de Ase todavía (ver AseDbContextOptionsBuilder) — cuando se agregue una opción
        // específica del provider (ej. estrategia de paginación, nivel de compatibilidad), copiarla acá.
    }

    public override DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    protected override RelationalOptionsExtension Clone()
        => new AseOptionsExtension(this);

    public override void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkAse();

    /// <summary>
    ///     <see cref="IDbContextOptionsExtension.ApplyDefaults" /> es un default interface method — sin este
    ///     passthrough explícito no queda visible al llamarlo sobre una referencia tipada
    ///     <see cref="AseOptionsExtension" /> (como hace <c>AseDbContextOptionsBuilderExtensions</c>). Ase
    ///     todavía no tiene defaults dinámicos que aplicar, así que por ahora es un no-op.
    /// </summary>
    public virtual IDbContextOptionsExtension ApplyDefaults(IDbContextOptions options)
        => this;

    // GetServiceProviderHashCode, ShouldUseSameServiceProvider y LogFragment se heredan de
    // RelationalExtensionInfo tal cual mientras Ase no tenga opciones propias que afecten al
    // service provider (ver AseDbContextOptionsBuilder) — solo PopulateDebugInfo es abstracto.
    private sealed class ExtensionInfo : RelationalExtensionInfo
    {
        public ExtensionInfo(IDbContextOptionsExtension extension)
            : base(extension)
        {
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["Ase"] = "1";
    }
}
