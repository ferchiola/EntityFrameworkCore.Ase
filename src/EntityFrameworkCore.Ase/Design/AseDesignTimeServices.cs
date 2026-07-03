using EntityFrameworkCore.Ase.Design.Internal;
using EntityFrameworkCore.Ase.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntityFrameworkCore.Ase.Design;

/// <summary>
///     Punto de entrada que las herramientas de <c>dotnet ef</c> (<c>dbcontext scaffold</c>) ubican vía
///     el atributo <c>[assembly: DesignTimeProviderServices(...)]</c> en <c>AssemblyInfo.cs</c> —
///     confirmado corriendo <c>dotnet-ef</c> real que, sin ese atributo, no la encuentran ni siendo
///     pública (el error es explícito: "Unable to find expected assembly attribute
///     [DesignTimeProviderServices]").
/// </summary>
public class AseDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        // El host de dotnet-ef arma un IServiceProvider propio y separado solo para scaffolding, así
        // que hace falta registrar el stack completo del provider acá (no solo lo específico de
        // scaffolding) — confirmado corriendo dotnet-ef real: sin esto, ScaffoldingTypeMapper falla
        // al no poder resolver IRelationalTypeMappingSource.
        serviceCollection.AddEntityFrameworkAse();

        // Registra las dependencias comunes de diseño (ProviderCodeGeneratorDependencies,
        // AnnotationCodeGeneratorDependencies, etc.) que dotnet-ef necesita para poder construir
        // AseCodeGenerator — sin esto falla con "Unable to resolve service for type
        // ProviderCodeGeneratorDependencies" (confirmado corriendo dotnet-ef real).
        new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection).TryAddCoreServices();

        serviceCollection.TryAddSingleton<IDatabaseModelFactory, AseDatabaseModelFactory>();
        serviceCollection.TryAddSingleton<IProviderConfigurationCodeGenerator, AseCodeGenerator>();
    }
}
