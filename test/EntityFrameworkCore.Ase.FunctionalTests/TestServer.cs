namespace EntityFrameworkCore.Ase.FunctionalTests;

/// <summary>
///     Host de la instancia real de ASE contra la que corren los tests funcionales. El default
///     (<c>SERVER_ASE</c>) es un hostname, no la IP real — no se documenta acá a propósito, para no
///     dejarla hardcodeada en un repo público (ver DECISIONS.md, Fase 4: ASE no escucha en loopback,
///     por eso no alcanza con <c>127.0.0.1</c>). Para que resuelva, hace falta una entrada en el
///     archivo <c>hosts</c> local apuntando <c>SERVER_ASE</c> a la IP real. En cualquier otra máquina
///     (o en CI), setear la variable de entorno <c>ASE_TEST_HOST</c> con el host/IP real antes de
///     ejecutar <c>dotnet test</c>.
/// </summary>
internal static class TestServer
{
    public static string Host => Environment.GetEnvironmentVariable("ASE_TEST_HOST") ?? "SERVER_ASE";
}
