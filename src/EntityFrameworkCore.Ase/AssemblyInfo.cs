using Microsoft.EntityFrameworkCore.Design;

// Requerido por dotnet-ef para scaffolding (dbcontext scaffold): sin este atributo, las herramientas
// no encuentran AseDesignTimeServices por más que sea pública en el assembly — confirmado corriendo
// dotnet-ef real, que tira "Unable to find expected assembly attribute [DesignTimeProviderServices]"
// si falta. El argumento tiene que ser un string literal (no typeof(...).FullName): los argumentos de
// atributo tienen que ser constantes de compilación.
[assembly: DesignTimeProviderServices("EntityFrameworkCore.Ase.Design.AseDesignTimeServices")]
