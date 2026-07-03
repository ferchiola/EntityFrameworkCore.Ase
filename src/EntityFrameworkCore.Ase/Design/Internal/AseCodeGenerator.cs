using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Scaffolding;

namespace EntityFrameworkCore.Ase.Design.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
/// <remarks>
///     Sin esto, el código C# generado por <c>dotnet ef dbcontext scaffold</c> no sabría qué método
///     <c>UseXxx</c> llamar en <c>OnConfiguring</c>/<c>Program.cs</c> — es el mismo rol que cumple
///     <c>SqlServerCodeGenerator</c>/<c>NpgsqlCodeGenerator</c> en sus respectivos providers.
///     <c>providerOptions</c> siempre llega <see langword="null" /> en la práctica: no overrideamos
///     <see cref="GenerateProviderOptions" />, así que la clase base nunca tiene nada que poner ahí
///     (no hay ninguna opción propia de Ase, como <c>UseRowNumberForPaging</c> en SQL Server, que
///     necesite aparecer en el <c>UseAse(...)</c> generado).
/// </remarks>
public class AseCodeGenerator : ProviderCodeGenerator
{
    public AseCodeGenerator(ProviderCodeGeneratorDependencies dependencies)
        : base(dependencies)
    {
    }

    public override MethodCallCodeFragment GenerateUseProvider(string connectionString, MethodCallCodeFragment? providerOptions)
        => new("UseAse", connectionString);
}
