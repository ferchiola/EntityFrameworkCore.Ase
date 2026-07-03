using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EntityFrameworkCore.Ase.Diagnostics.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release.
/// </summary>
/// <remarks>
///     Marker type used by EF Core to scope warning/event IDs to this provider. No members propios
///     todavía — event IDs específicos de Ase se agregan acá a medida que hagan falta en fases
///     siguientes.
///     <para>
///         <b>Bug real encontrado en Fase 4</b> (ver DECISIONS.md): originalmente heredaba de
///         <see cref="LoggingDefinitions" /> a secas, copiando la firma pública de la clase base sin
///         verificar qué necesita el pipeline relacional en tiempo de ejecución. Rompía con
///         <see cref="InvalidCastException" /> apenas se ejecutaba una query real («Unable to cast
///         object of type 'AseLoggingDefinitions' to type 'RelationalLoggingDefinitions'») porque
///         <c>RelationalResources</c> castea el logger a <see cref="RelationalLoggingDefinitions" />
///         internamente. No se detectó en la Fase 1 porque los tests de esa fase no llegaban a
///         ejecutar ninguna query real — otro caso más de por qué hace falta probar contra ASE de
///         verdad, no solo compilar.
///     </para>
/// </remarks>
public class AseLoggingDefinitions : RelationalLoggingDefinitions
{
}
