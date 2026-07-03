namespace EntityFrameworkCore.Ase.FunctionalTests;

/// <summary>
///     Host de la instancia real de ASE contra la que corren los tests funcionales. El default
///     (<c>127.0.0.1</c>) es deliberadamente no funcional — ASE en la instancia de desarrollo no
///     escucha en loopback, solo en su IP de LAN (ver DECISIONS.md, Fase 4) — para no dejar esa IP
///     real hardcodeada en un repo público. Para correr estos tests localmente, setear la variable de
///     entorno <c>ASE_TEST_HOST</c> con la IP/host real antes de ejecutar <c>dotnet test</c>.
/// </summary>
internal static class TestServer
{
    public static string Host => Environment.GetEnvironmentVariable("ASE_TEST_HOST") ?? "127.0.0.1";
}
