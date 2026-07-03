# DECISIONS.md

Registro de decisiones de diseño, por fase. Qué se decidió, por qué, y qué
alternativas se descartaron.

## Fase 0 — Investigación y setup

### Decisiones confirmadas con el usuario

- **Versión de SAP ASE objetivo: 16.x.** No se soportan versiones anteriores
  (15.x) por ahora. Simplifica sobre todo paginación (ver abajo).
- **Framework: .NET 9 / EF Core 9** (`Microsoft.EntityFrameworkCore.Relational`).
  Se descartó .NET 10 por ahora por menor adopción en producción; se podría
  agregar multi-target más adelante si hace falta.
- **Driver ADO.NET: `AdoNetCore.AseClient`** (Apache-2.0, DataAction). Se
  descartó el driver oficial de SAP (`Sybase.Data.AseClient` /
  `SAP.Data.AseClient`) porque depende de COM y es exclusivo de .NET
  Framework — incompatible con .NET moderno multiplataforma.

### Paquete NuGet ya existente con el mismo nombre

Ya existe `EntityFrameworkCore.Ase` publicado en NuGet.org (autor
`spasarto`, repo [github.com/spasarto/EntityFrameworkCore.Ase](https://github.com/spasarto/EntityFrameworkCore.Ase)),
sin actividad desde octubre 2022, **sin licencia declarada** (`license: null`
en la API de GitHub). Mismo estado en el fork
[JiarongGu/Rissole.EntityFrameworkCore.Ase](https://github.com/JiarongGu/Rissole.EntityFrameworkCore.Ase).

- Se usó ese repo **únicamente como referencia de arquitectura** (confirma
  que el enfoque estándar de EF Core — `RelationalConnection`,
  `RelationalOptionsExtension`, etc. — aplica igual acá, y que también usa
  `AdoNetCore.AseClient` como driver). **No se copió código** de ese repo,
  por la falta de licencia.
- **Pendiente**: definir un nombre de paquete propio antes de publicar en
  NuGet.org (ej. `<TuOrg>.EntityFrameworkCore.Ase`), ya que el nombre
  `EntityFrameworkCore.Ase` a secas está tomado. El nombre de la solución/
  namespace interno puede seguir siendo `EntityFrameworkCore.Ase` mientras
  el desarrollo es privado.

### Particularidades de SAP ASE 16 relevantes para el provider

**Paginación.** ASE 16 (a partir de aprox. 16.0.2.x) soporta sintaxis
estándar `OFFSET n ROWS FETCH NEXT m ROWS ONLY`, igual que SQL Server
moderno — mejor de lo esperado. Versiones anteriores (15.7) no la tenían y
dependían de `SET ROWCOUNT` / `TOP`. **A verificar contra la instancia real
del usuario** antes de dar esto por sentado en la Fase 4 (confirmar el
service pack exacto).

**Identity / autoincrement.** Columnas `IDENTITY` (tipo numeric/entero,
scale 0), autogeneradas, obtenibles con la variable global `@@identity`
(scope de sesión) o el keyword `syb_identity`. A diferencia de SQL Server,
no se encontró evidencia de un equivalente a `SCOPE_IDENTITY()` que evite
problemas con triggers — **a confirmar en Fase 2/3 contra la instancia
real**; si no existe, documentar la limitación.

**Delimitadores de identificadores.** ASE soporta tanto corchetes `[...]`
como comillas dobles `"..."`, pero las comillas dobles requieren
`SET QUOTED_IDENTIFIER ON` a nivel de sesión (y una vez activado, los
literales string deben ir en comillas simples exclusivamente). **Decisión:
usar corchetes `[...]` como delimitador por defecto** (igual que SQL
Server) para no depender de un `SET` adicional en cada conexión. Límite de
longitud de identificador: 253 caracteres (con "quoted identifier
enhancement") o 255 sin ella — no es una restricción práctica.

**Tipos de datos.**
- `bigdatetime`: entero de 64 bits, microsegundos desde 0000-01-01 — buen
  candidato para mapear `DateTime`/`DateTimeOffset` con precisión alta.
- `unitext`/`text`: tipos de texto largo (Unicode/no Unicode).
- **No existe un equivalente nativo a `rowversion`/`timestamp` de SQL
  Server** (columna binaria auto-incremental para concurrencia optimista).
  Confirmado por foros oficiales de SAP. **Hay que resolverlo con shadow
  property + trigger** (Fase 3), como ya preveía el plan original.

**Aislamiento de transacciones.** Nivel 1 (default, evita dirty reads) es
el estándar. Nivel 2 (evita nonrepeatable reads) **solo está soportado en
tablas con locking data-only-locked, no en allpages-locked** — matiz a
tener en cuenta si el usuario necesita ese nivel. Nivel 3 (serializable) se
logra con el keyword `holdlock`.

**Metadata de esquema.** Catálogos `sysobjects` (`type='U'` tablas de
usuario, `'S'` tablas de sistema) y `syscolumns` — equivalentes pre-ANSI a
los viejos catálogos de Sybase/SQL Server, sin `INFORMATION_SCHEMA`
completo. Van a ser la base para scaffolding/reverse engineering más
adelante (no priorizado en este plan).

### Estructura de la solución

Creada: `src/EntityFrameworkCore.Ase` (classlib, net9.0),
`test/EntityFrameworkCore.Ase.Tests` (xUnit), `test/EntityFrameworkCore.Ase.FunctionalTests`
(xUnit, para los specification tests de la Fase 8), con
`Microsoft.EntityFrameworkCore.Relational` y `AdoNetCore.AseClient`
instalados en el proyecto del provider. Todo compila sin errores.

## Fase 1 — Registro de servicios (DI) y "Use..." method

### Qué se implementó

- `AseOptionsExtension : RelationalOptionsExtension` (`Infrastructure/Internal/`) — `Info`,
  `Clone`, `ApplyServices` (delega a `AddEntityFrameworkAse`), constructor de copia. Se agregó
  además un passthrough explícito de `ApplyDefaults` (ver nota técnica abajo).
- `AseDbContextOptionsBuilder` (`Infrastructure/`) — por ahora vacía; no hay todavía ninguna
  opción específica de Ase que exponer (candidatas para más adelante: nivel de compatibilidad
  ASE, estrategia de paginación).
- `AseDbContextOptionsBuilderExtensions.UseAse(...)` (`Extensions/`) — overloads por connection
  string, `DbConnection` (con/sin ownership) y sin argumentos, más los genéricos `UseAse<TContext>`
  equivalentes. Sigue el mismo patrón que `SqlServerDbContextOptionsExtensions.UseSqlServer`.
- `AseServiceCollectionExtensions.AddAse<TContext>` y `.AddEntityFrameworkAse()` (`Extensions/`).
- `AseLoggingDefinitions : LoggingDefinitions` (`Diagnostics/Internal/`) — marcador vacío por ahora.

Se auditó (sin copiar) `SqlServerOptionsExtension`, `SqlServerDbContextOptionsBuilder`,
`SqlServerDbContextOptionsBuilderExtensions.cs` y `SqlServerServiceCollectionExtensions.cs` del
repo oficial `dotnet/efcore` (rama `release/9.0`, MIT) para confirmar la forma actual de estas
piezas — la guía de Microsoft referenciada en el CLAUDE.md está desactualizada (EF Core 1.1) y la
API real cambió bastante desde entonces.

### Nota técnica: `ApplyDefaults` es un default interface method

`IDbContextOptionsExtension.ApplyDefaults(IDbContextOptions)` tiene una implementación default en
la interfaz (`=> this;`). `RelationalOptionsExtension` no la re-declara, así que **no es visible
llamándola sobre una referencia tipada `AseOptionsExtension`** (los default interface methods solo
son accesibles a través del tipo de la interfaz, salvo que la clase provea su propio miembro
público con la misma firma). SqlServer resuelve esto declarando su propio
`public virtual IDbContextOptionsExtension ApplyDefaults(...)` — se hizo lo mismo acá, como no-op
por ahora (Ase no tiene defaults dinámicos que aplicar todavía).

### Sobre los lifetimes de los servicios registrados

La consigna original pedía justificar el lifetime (singleton/scoped) elegido para cada servicio
registrado. **Corrección importante tras auditar el código real de EF Core**: el lifetime de la
mayoría de los servicios de extensión (`LoggingDefinitions`, `IDatabaseProvider`, y todo lo que se
registra vía `.TryAdd<TService, TImpl>()`) **no lo elige el provider** — está fijado de forma
centralizada por EF Core mismo, en el diccionario `EntityFrameworkServicesBuilder.CoreServices` (y
el equivalente `RelationalServicesBuilder.RelationalServices` para las piezas relacionales). El
provider solo *implementa* la interfaz; EF Core decide con qué lifetime exponerla.

Los dos servicios que registramos en esta fase:

- `LoggingDefinitions` → **Singleton** (fijado por EF Core). Tiene sentido: no tiene estado por
  request, solo define qué event IDs usa el provider para logging.
- `IDatabaseProvider` → **Singleton, multipleRegistrations: true** (fijado por EF Core). También
  tiene sentido: es solo una marca de "qué provider fue seleccionado", sin estado, y varios
  providers pueden convivir registrados en el mismo `IServiceCollection`.

El provider **sí elige el lifetime** para sus propios servicios internos que EF Core no conoce de
antemano — eso se hace con `.TryAddProviderSpecificServices(b => b.TryAddSingleton<...>()/...TryAddScoped<...>())`,
como hace SqlServer con `ISqlServerConnection` (Scoped — una conexión por instancia de
`DbContext`) y `ISqlServerSingletonOptions`/`ISqlServerValueGeneratorCache`/`ISqlServerUpdateSqlGenerator`
(Singleton — sin estado por request, se comparten). Todavía no usamos
`TryAddProviderSpecificServices` porque no hay ningún servicio *propio* de Ase implementado aún —
va a aparecer recién en la Fase 2 con la conexión (`IAseConnection`, previsiblemente Scoped, igual
que en SqlServer).

### Servicios pendientes (dejados como TODO en `AseServiceCollectionExtensions`)

Comentados con `.TryAdd<...>()` y agrupados por la fase que los va a implementar (conexión,
type mapping, SQL generation, migraciones, metadata/annotations) — ver el archivo directamente.
Sin estas piezas, `UseAse(...)` arma correctamente las `DbContextOptions` pero todavía no alcanza
para ejecutar una query real contra ASE (eso empieza en la Fase 2).

### Verificado con tests

`test/EntityFrameworkCore.Ase.Tests/Infrastructure/UseAseTests.cs`: `UseAse(connectionString)`
deja el `AseOptionsExtension` correcto en `DbContextOptions`, el overload genérico encadena bien,
y `AddEntityFrameworkAse()` deja `IDatabaseProvider` resolvible desde el `IServiceProvider`. Los 3
tests pasan.

## Fase 2 — Conexión

### Qué se implementó

- `IAseConnection : IRelationalConnection` y `AseRelationalConnection : RelationalConnection` (`Storage/Internal/`)
  — solo overridea `CreateDbConnection()` devolviendo `new AseConnection(GetValidatedConnectionString())`
  del driver `AdoNetCore.AseClient`. No fue necesario overridear `OpenDbConnection` (el hack de
  `SqlConnectionOverrides.OpenWithoutRetry` que hace SqlServer es específico de `Microsoft.Data.SqlClient`,
  no aplica acá).
- Registrado en `AseServiceCollectionExtensions`: `IRelationalConnection` → `IAseConnection` (vía
  `TryAdd`, dictado por EF Core) y `IAseConnection` → `AseRelationalConnection` como **Scoped**, vía
  `TryAddProviderSpecificServices` — acá sí lo elegimos nosotros (ver Fase 1): Scoped porque cada
  `DbContext` necesita su propia conexión/transacción, igual que `ISqlServerConnection`.

### Decisiones

- **Connection pooling**: se delega enteramente al driver. `AdoNetCore.AseClient` soporta
  `Pooling`, `Max Pool Size` y `Min Pool Size` directamente como keywords del connection string
  (confirmado en su README) — el connection string se pasa intacto (`GetValidatedConnectionString()`)
  sin parsearlo ni tocarlo, así que pooling "funciona solo" salvo que el usuario lo desactive
  explícitamente. No hay ninguna lógica de pooling propia que escribir.
- **Ambient transactions** (`SupportsAmbientTransactions`): se dejó el default de la clase base
  (`false`), NO el `true` que usa SqlServer. No hay documentación ni evidencia de que
  `AdoNetCore.AseClient` soporte `System.Transactions` — asumir que sí porque SQL Server lo hace
  sería exactamente el tipo de suposición que la consigna pide evitar.
- **`CreateMasterConnection()`** (que SqlServer expone para crear/borrar la base desde una conexión
  a `master`): no se implementó todavía — es un método de `ISqlServerConnection`, no de la interfaz
  base `IRelationalConnection`, y su primer uso real es `AseDatabaseCreator` en la Fase 5. Se agrega
  ahí para no adelantar diseño sin el contexto completo (ASE probablemente use una base de
  administración distinta a `master`, a confirmar contra la instancia real en esa fase).

### Tests (con mocks, sin ASE real — como pedía la consigna)

`test/EntityFrameworkCore.Ase.Tests/Storage/AseRelationalConnectionTests.cs`. Se construyó
`RelationalConnectionDependencies` a mano mockeando sus 6 dependencias con Moq (en vez de replicar
la infraestructura interna `TestUtilities` de EF Core, que no es un paquete público consumible).

**Trampa real encontrada y documentada para el futuro**: `RelationalConnection` llama
SIEMPRE a `Dependencies.ConnectionStringResolver.ResolveConnectionString(...)` en su constructor —
no solo para connection strings con formato `Name=...`. Un mock "loose" de
`INamedConnectionStringResolver` (`Mock.Of<T>()` sin `Setup`) devuelve `null` por defecto, lo que
rompe silenciosamente el connection string entero (queda `null`) sin ninguna excepción. Hubo que
configurar el mock como passthrough explícito (`.Setup(...).Returns((string s) => s)`). Si en fases
futuras aparecen más tests con `RelationalConnectionDependencies` mockeadas, tenerlo en cuenta.

Los 3 tests verifican: que `CreateDbConnection()` devuelve un `AseConnection` con el connection
string correcto, que `AseRelationalConnection.ConnectionString` lo expone igual, y que los
keywords de pooling llegan intactos al driver. No se prueba que el pooling "funcione" de punta a
punta (requiere ASE real — Fase 8).

## Fase 3 — Type Mapping

### Qué se implementó

`AseTypeMappingSource : RelationalTypeMappingSource` (`Storage/Internal/`), registrado como
`IRelationalTypeMappingSource` (Singleton, dictado por EF Core). Cubre exactamente la lista pedida:
bool, byte, short, int, long, decimal, double, float, DateTime, DateTimeOffset, Guid, string,
byte[].

**Hallazgo clave que cambió el plan de la Fase 0**: muchos tipos genéricos de EF Core
(`BoolTypeMapping`, `ByteTypeMapping`, `ShortTypeMapping`, `IntTypeMapping`, `LongTypeMapping`,
`FloatTypeMapping`, `DateTimeTypeMapping`) ya tienen un `.Default` cuyo nombre de store type
(`bit`, `tinyint`, `smallint`, `int`, `bigint`, `float`, `datetime`) **coincide exactamente** con el
tipo real de ASE — se reusan tal cual, sin ninguna clase propia. Solo hicieron falta mapeos
custom para los casos donde el nombre por defecto de EF Core no sirve o ASE no tiene el tipo:

| CLR | Store type Ase | Por qué necesitó código propio |
|---|---|---|
| `double` | `double precision` | `DoubleTypeMapping.Default` es `"double"`, no es sintaxis válida de ASE |
| `Guid` | `binary(16)` | ASE no tiene GUID/UUID nativo; el driver acepta `DbType.Guid` y convierte a binario por debajo |
| `decimal` | `decimal(18,2)` | Necesita `StoreTypePostfix.PrecisionAndScale` — ver nota técnica abajo |
| `string` | `varchar`/`univarchar`/`text`/`unitext` según longitud y Unicode | Necesita `StoreTypePostfix.Size` + lógica de bounded/unbounded |
| `byte[]` | `varbinary`/`binary`/`image` según longitud | Igual que `string` |
| `DateTimeOffset` | `datetime` (con conversión) | Ver limitación abajo |

### Nota técnica: por qué hicieron falta `AseStringTypeMapping`/`AseByteArrayTypeMapping`/`AseDecimalTypeMapping`

Los constructores públicos de `StringTypeMapping`, `ByteArrayTypeMapping` y `DecimalTypeMapping` (y
el constructor "de conveniencia" de `RelationalTypeMapping` del que dependen) **fuerzan
`StoreTypePostfix.None`** — ni siquiera pasándoles `size`/`precision`/`scale` el tipo generado en el
DDL incluye esos valores entre paréntesis. Hay que pasar por el constructor `protected` que recibe
`RelationalTypeMappingParameters` directamente para poder pedir `StoreTypePostfix.Size` o
`.PrecisionAndScale` explícitamente. Se creó una subclase mínima para cada uno (mismo patrón que
usa `SqlServerStringTypeMapping`/etc., pero sin las partes específicas de `SqlDbType` de
`Microsoft.Data.SqlClient`, que no aplican acá).

### Limitaciones conocidas (documentadas, no escondidas)

- **`DateTimeOffset` pierde el offset original.** ASE no tiene tipo equivalente y el driver lo
  confirma explícitamente. Se convierte a `DateTime` UTC para guardar y se reconstruye con offset
  cero al leer — preserva el instante exacto, no el offset. Si algún proyecto necesita el offset
  tal cual, tiene que modelarlo con columnas separadas, no usar este mapeo. Ver
  `AseDateTimeOffsetTypeMapping` para el detalle.
- **`bigdatetime` descartado como store type para `DateTime`**, a pesar de que la Fase 0 lo había
  propuesto como buen candidato por su precisión de microsegundos. Se corrige acá: el driver
  `AdoNetCore.AseClient` **todavía no implementa la lectura de `bigdatetime`** ("To be implemented"
  en su tabla de tipos soportados) — usarlo hubiera roto cualquier lectura. Se usa `datetime`
  estándar, sí soportado en ambas direcciones.
- **Rowversion / concurrency token: sigue sin resolverse**, tal como se documentó en la Fase 0. No
  hay ningún mapeo de tipo "rowversion" en `AseTypeMappingSource` porque ASE no tiene nada
  equivalente — implementar el workaround de shadow property + trigger requiere migraciones
  (Fase 5) y posiblemente metadata/annotations (Fase 6), no es una pieza de type mapping en sí.
  Queda pendiente para cuando esas fases existan.
- **Guid → `binary(16)` sin verificar contra ASE real todavía.** El README del driver dice que
  soporta `DbType.Guid` convirtiendo a binario "por debajo", pero no se probó el round-trip
  (insertar un Guid y leerlo de vuelta) contra una instancia real — queda para la Fase 8 (o antes,
  si en Fase 4/5 se prueba una tabla con una columna Guid).
- **`MaxBoundedLength = 16384`** (umbral para pasar de `varchar`/`varbinary` a `text`/`image`) es un
  valor típico documentado por SAP, no confirmado contra la instancia real del usuario — a
  verificar cuando se pruebe con datos reales.

### Tests (tipo por tipo, sin ASE real)

`test/EntityFrameworkCore.Ase.Tests/Storage/AseTypeMappingSourceTests.cs`. Se evitó a propósito
construir un `DbContext` real completo (`context.Model`) para probar los mapeos de `string`/`byte[]`
con distintos tamaños — dispara la resolución de `ISqlGenerationHelper`, que es de la Fase 4 y
todavía no existe, y tira `InvalidOperationException`. En cambio se usó el overload público
`IRelationalTypeMappingSource.FindMapping(Type, string? storeTypeName, ..., unicode, size, ...)`,
que no necesita ningún `Property`/`Model` real. 29 tests, cubren: los 11 tipos CLR de la lista
pedida, la conversión de `DateTimeOffset` (incluyendo que se pierde el offset), que `Guid` use
`DbType.Guid`, `string`/`byte[]` en sus 4 combinaciones (unicode/ansi × bounded/unbounded), y
resolución por nombre de store type explícito (`HasColumnType(...)`/scaffolding).

## Fase 4 — SQL Generation (queries)

Esta fue la fase más grande hasta ahora, y la primera con acceso a una instancia real de SAP ASE
(16.1.0, local). Varias decisiones de fases anteriores se verificaron o corrigieron acá.

### Hallazgo previo: acceso real a ASE

El usuario indicó tener acceso a una instancia real (recién en esta fase se compartieron los datos:
puerto 5000, motor ASE 16.1.0, corriendo localmente en la misma máquina donde se ejecuta Claude Code —
el proceso `sqlsrvr` escucha en la IP LAN de la máquina, no en `localhost`/127.0.0.1; la IP real no se
documenta acá a propósito, ver `TestServer.Host` en los tests funcionales). Esto permitió reemplazar
suposiciones basadas en documentación web por verificación directa.

**Nota de seguridad:** las credenciales de esa instancia quedaron hardcodeadas en
`test/EntityFrameworkCore.Ase.FunctionalTests/Query/BasicQueryTests.cs` para poder correr los tests
de integración. Si este repo se sube a control de versiones o se comparte, hay que sacarlas de ahí
(variable de entorno o user-secrets) — no se hizo todavía porque no se pidió explícitamente y el
repo no tiene git inicializado.

### Descubrimiento importante: el driver necesita un `EncodingProvider` registrado

Al conectar contra la instancia real por primera vez aparecía
`AseException: Server environment changed to unsupported charset 'cp850'` — el charset default del
server (común en instalaciones de ASE en Windows) no lo entiende `AdoNetCore.AseClient` sin que el
proceso tenga registrado `System.Text.Encoding.CodePages`. Se agregó
`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` en el constructor estático de
`AseRelationalConnection` (Fase 2, corregido acá) para que cualquier consumidor del provider no
tenga que enterarse de este detalle. Se agregó el paquete `System.Text.Encoding.CodePages` a
`EntityFrameworkCore.Ase.csproj`.

### Paginación: verificada y corregida contra ASE real

La Fase 0 había encontrado (por búsqueda web, sin verificar) `OFFSET n ROWS FETCH NEXT m ROWS ONLY`
como sintaxis probable. **Era incorrecto.** Se probó exhaustivamente contra la instancia real:

| Sintaxis probada | Resultado |
|---|---|
| `TOP n` (sin paréntesis) | ✅ funciona |
| `TOP(n)` (estilo SQL Server moderno) | ❌ "Incorrect syntax near the keyword 'FROM'" |
| `OFFSET n ROWS FETCH NEXT m ROWS ONLY` (SQL Server) | ❌ "Incorrect syntax near 'OFFSET'" |
| `LIMIT n OFFSET m` (MySQL/Postgres) | ❌ "Incorrect syntax near 'LIMIT'" |
| `ORDER BY ... ROWS OFFSET n LIMIT m` | ✅ funciona, con o sin `ORDER BY`, con Skip-only (`ROWS OFFSET n` sin `LIMIT`) |
| `TOP @parametro` / `TOP (@parametro)` | ❌ "Incorrect syntax" — **`TOP` no acepta parámetros, solo literales** |
| `ROWS OFFSET @o LIMIT @n` (parametrizado) | ✅ funciona |

**Decisión final:** `AseQuerySqlGenerator` no usa `TOP` para nada, ni siquiera para "Take sin Skip".
Como EF Core parametriza `Take(n)`/`Skip(n)` por defecto (para cachear el plan de query),
`TOP` sería inutilizable en la práctica real. Todo pasa por `ROWS OFFSET {offset} LIMIT {limit}`,
usando `OFFSET 0` cuando no hay `Skip`. A diferencia de `SqlServerQuerySqlGenerator`, no hace falta
inyectar un `ORDER BY (SELECT 1)` de relleno — ASE no exige `ORDER BY` para paginar.

### Bug real encontrado y corregido: `AseLoggingDefinitions`

Heredaba de `LoggingDefinitions` (Fase 1) en vez de `RelationalLoggingDefinitions`. Compilaba y
pasaba los tests de la Fase 1 (que nunca ejecutaban una query real), pero rompía con
`InvalidCastException` en la primera query real contra ASE, porque `RelationalResources` castea el
logger a `RelationalLoggingDefinitions` internamente. Corregido. Otro recordatorio de por qué "esto
compila y pasa los tests" no es lo mismo que "esto funciona" — hasta que no se corrió una query real
no se detectó.

### Bug propio encontrado y corregido: `StartsWith`/`EndsWith` invertidos

En `AseSqlTranslatingExpressionVisitor`, la primera versión armaba el patrón LIKE con la lógica de
prefijo/sufijo cambiada entre `StartsWith` y `EndsWith` (`Contains` funcionaba "por casualidad"
porque ahí ambos flags dan el mismo resultado esté o no invertido). Se detectó porque el test de
`StartsWith` devolvía 0 filas en vez de 1 — otro caso a favor de probar contra datos reales, no
confiar en que "compila y el patrón se ve razonable".

### Qué se implementó

- `AseSqlGenerationHelper` — delimitadores `[corchetes]` (Fase 0, confirmado que también funcionan
  comillas dobles sin `SET QUOTED_IDENTIFIER`, pero se mantiene la decisión original), batch
  terminator `GO`, `BEGIN TRANSACTION` (ASE no soporta `START TRANSACTION` ANSI), savepoints con
  `SAVE TRANSACTION`/`ROLLBACK TRANSACTION <name>` (no el `SAVEPOINT`/`ROLLBACK TO` ANSI de la
  clase base) — todo confirmado contra la instancia real. `GenerateReleaseSavepointStatement`
  lanza `NotSupportedException` (sin evidencia de que ASE lo soporte, mismo criterio que SQL Server).
- `AseQuerySqlGenerator` + `AseQuerySqlGeneratorFactory` — paginación (ver arriba).
- `AseSqlTranslatingExpressionVisitor` + su factory — traduce `string.Contains/StartsWith/EndsWith`
  a `LIKE`. Ningún traductor genérico de EF Core cubre esto (se auditó
  `SqlServerSqlTranslatingExpressionVisitor` para confirmarlo — SQL Server tampoco lo resuelve vía
  `IMethodCallTranslator`). Versión más simple que la de SQL Server: para patrones constantes arma
  un literal LIKE con escape de `%`/`_`; para patrones no constantes (parámetros) usa concatenación
  SQL (`'%' + patrón + '%'`, confirmado que `+` concatena en ASE) **sin** escapar wildcards — si el
  valor en runtime tiene un `%`/`_` literal, se interpreta como wildcard. Limitación conocida y
  documentada, no se replicó la lógica de SQL Server de recalcular el parámetro en tiempo de
  ejecución.
- `AseStringMethodTranslator` — `ToUpper`/`ToLower`/`Trim`/`TrimStart`/`TrimEnd` vía
  `UPPER`/`LOWER`/`LTRIM`/`RTRIM`, todo confirmado idéntico a T-SQL.
- `AseDateTimeMethodTranslator` — `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds` de
  `DateTime` vía `DATEADD(datepart, n, fecha)`, confirmado idéntico a T-SQL. No cubre
  `DateTimeOffset` (ya se convierte a `DateTime` a nivel de type mapping, Fase 3).
- `AseMemberTranslator` — `string.Length` vía `LEN()`, confirmado.
- `IQueryableMethodTranslatingExpressionVisitorFactory`: se registró la clase **genérica** de EF
  Core (`RelationalQueryableMethodTranslatingExpressionVisitorFactory`) sin ninguna subclase propia
  — no hay ninguna forma de query shape (joins, groupby, etc.) que ASE resuelva distinto de lo
  genérico, al menos para los casos probados.

### Piezas agregadas que NO pedía la Fase 4, pero eran necesarias igual

Al armar los tests de integración reales se descubrió que EF Core arma el árbol completo de
servicios de un `DbContext` **incluso para correr una sola query de lectura** — falla al construir
si falta cualquier servicio de escritura/validación/convenciones, aunque nunca se llame
`SaveChanges()`. Se agregaron versiones mínimas, varias reutilizando clases genéricas de EF Core sin
ninguna lógica propia:

- `IModelValidator` → `RelationalModelValidator` (genérica, sin subclase).
- `IValueGeneratorSelector` → `RelationalValueGeneratorSelector` (genérica, sin subclase).
- `IProviderConventionSetBuilder` → `AseConventionSetBuilder` (subclase vacía — la base ya arma el
  convention set completo, es `abstract` solo por diseño, no porque falte implementar algo).
- `IUpdateSqlGenerator` → `AseUpdateSqlGenerator` (subclase vacía de `UpdateSqlGenerator`, que
  tampoco tiene miembros abstractos reales — **no probado contra `SaveChanges()` real**, eso es
  contenido genuino de una fase posterior).
- `IModificationCommandFactory` → `ModificationCommandFactory` (genérica, sin subclase).
- `IModificationCommandBatchFactory` → `AseModificationCommandBatchFactory`, usa
  `SingularModificationCommandBatch` (un comando por round-trip, sin agrupar) — no se verificó si
  ASE soporta batching de varios INSERT/UPDATE/DELETE en un mismo mensaje, se eligió la opción
  conservadora en vez de asumir que sí (mismo criterio que usa el provider de SQLite).
- `IRelationalDatabaseCreator` → `AseDatabaseCreator`, con los 4 métodos (`Exists`, `Create`,
  `Delete`, `HasTables`) lanzando `NotImplementedException` — placeholder puro, la implementación
  real es contenido de la Fase 5.

**Importante:** estas piezas dejan el `DbContext` "arrancable" para queries de lectura, pero
`SaveChanges()` (INSERT/UPDATE/DELETE) y `EnsureCreated()`/`EnsureDeleted()`/migraciones **no están
probados ni garantizados** — quedan explícitamente para la Fase 5.

### Tests

`test/EntityFrameworkCore.Ase.FunctionalTests/Query/BasicQueryTests.cs` — 8 tests de integración
real contra ASE (no mocks): `Where`+`OrderBy`, Take-only, Skip+Take, Skip-only, `Contains`,
`StartsWith`, `ToUpper`+`Length`, join simple. Los datos se siembran con SQL crudo
(`ExecuteSqlRaw`), no con `SaveChanges()` (todavía no implementado, ver arriba). Los 8 pasan contra
la instancia real. Los 29 tests unitarios de fases anteriores (con mocks) también siguen pasando —
37 tests en total, 0 fallos.

## Fase 5 — Migraciones y Database Creator

Implementados `AseMigrationsSqlGenerator`, `AseDatabaseCreator` y `AseHistoryRepository`, y probados
de punta a punta contra la instancia real de ASE con un test que hace `Database.Migrate()` completo
(no `EnsureCreated()`): crea la base, crea `__EFMigrationsHistory`, aplica una migración real que crea
una tabla con columna `IDENTITY`, inserta una fila y confirma que el historial quedó registrado.

### `AseDatabaseCreator`

- `Create()`: `CREATE DATABASE {nombre}` contra una conexión a `master` (`master` es la base
  administrativa en ASE, igual que en SQL Server). Además habilita dos dboptions sobre la base recién
  creada, ambas confirmadas como necesarias contra ASE real:
  - `select into/bulkcopy`: sin esto, `ALTER TABLE ... DROP COLUMN` falla ("Neither the 'select into'
    nor the 'full logging for alter table' database options are enabled").
  - `ddl in tran`: sin esto, cualquier `CREATE TABLE` ejecutado dentro de una transacción explícita
    falla ("'CREATE TABLE' command is not allowed within a multi-statement transaction") — y
    `HistoryRepository.CreateIfNotExists()` de EF Core siempre envuelve la creación de
    `__EFMigrationsHistory` en una transacción.
- **Bug real y su fix — nombre de base nulo en `Create()`**: usar
  `_connection.DbConnection.Database` para obtener el nombre de la base a crear generaba
  `CREATE DATABASE` sin nombre ("Incorrect syntax near 'DATABASE'"). Causa: en
  `AdoNetCore.AseClient`, `AseConnection.Database` depende de un objeto interno del driver que solo
  existe una vez que la conexión está *realmente abierta* — antes de eso devuelve `null` —, y `Create()`
  se invoca precisamente antes de abrir la conexión (porque la base todavía no existe). Fix: parsear
  el nombre de la base directamente del connection string con
  `System.Data.Common.DbConnectionStringBuilder` (`AseConnectionStringBuilder` del driver es
  `internal sealed`, no se puede usar).
- `Delete()`: necesita `AseConnection.ClearPools()` antes de `DROP DATABASE`, si no falla con
  "Cannot drop or replace the database ... because it is currently in use" apenas hubo alguna
  conexión pooleada a esa base. Hay un retry (15 intentos, 500ms) como red de seguridad, pero **no es
  una solución completa** — ver hallazgo de pooling más abajo.
- `Exists()`: intenta abrir la conexión (`errorsExpected: true`) y la cierra; si tira `AseException`,
  la base no existe. No se encontró (ni se buscó agresivamente) un catálogo equivalente a
  `sys.databases` de SQL Server accesible sin permisos elevados, así que se optó por este enfoque más
  simple, igual de válido.
- `HasTables()`: `SELECT COUNT(*) FROM sysobjects WHERE type = 'U'` — `sysobjects` con `type='U'`
  (tablas de usuario) es un catálogo real de Sybase/ASE, confirmado contra la instancia real.

### `AseMigrationsSqlGenerator`

- **Columnas `IDENTITY`**: `int NOT NULL IDENTITY` falla contra ASE real — el `NOT NULL` (o `NULL`)
  no puede coexistir con `IDENTITY` en la misma definición de columna. Confirmado que `int IDENTITY`
  a secas sí funciona (una columna `IDENTITY` en ASE es implícitamente `NOT NULL`). El generador
  detecta la anotación `AseAnnotationNames.ValueGenerationStrategy == IdentityColumn` y en ese caso
  omite completamente la cláusula `NULL`/`NOT NULL`, agregando solo `IDENTITY` al final.
- **`DROP INDEX`**: ASE requiere la sintaxis con punto `DROP INDEX {tabla}.{índice}` (no
  `DROP INDEX {índice} ON {tabla}` como SQL Server, ni `DROP INDEX {índice}` a secas). La clase base
  de EF Core (`MigrationsSqlGenerator`) tira `NotSupportedException` para `DropIndexOperation` por
  diseño (delega la sintaxis exacta a cada provider) — se sobreescribió `Generate(DropIndexOperation)`
  con la sintaxis verificada.
- Nueva convención `AseValueGenerationStrategyConvention` (`IModelFinalizingConvention`): para cada
  propiedad declarada con `ValueGenerated.OnAdd`, tipo CLR entero (`int`/`long`/`short`, desenvolviendo
  `Nullable<T>`), sin valor default ni SQL computado, marca automáticamente la anotación
  `IdentityColumn` — así una entidad con `int Id { get; set; }` como PK se comporta como `IDENTITY`
  sin que el usuario tenga que configurarlo a mano, igual que en SQL Server.

### `AseHistoryRepository`

- `ExistsSql` y `GetCreateIfNotExistsScript()` usan el patrón `OBJECT_ID(...)` /
  `IF (NOT) EXISTS (...) BEGIN...END`.
- **Bug real y su fix — `OBJECT_ID` no acepta identificadores entre corchetes**: a diferencia de un
  identificador usado dentro de una sentencia SQL (donde `[corchetes]` sí son válidos como
  delimitador, ver `AseSqlGenerationHelper` de la Fase 4), `OBJECT_ID(...)` espera el nombre del
  objeto como *string plano*, sin delimitadores. Confirmado contra ASE real con una prueba directa:
  `SELECT OBJECT_ID('[__EFMigrationsHistory]')` devuelve `NULL` siempre (como si el objeto nunca
  existiera), mientras que `SELECT OBJECT_ID('__EFMigrationsHistory')` (sin corchetes) devuelve el id
  real. Este bug era invisible a simple vista porque no tiraba ningún error — `Exists()` simplemente
  devolvía `false` siempre, así que `GetAppliedMigrations()` devolvía una lista vacía aun después de
  que la migración se aplicara y la fila quedara insertada en la tabla (confirmado leyendo la fila
  directamente por SQL crudo mientras el bug seguía presente). Fix: armar el nombre sin pasar por
  `SqlGenerationHelper.DelimitIdentifier`, solo concatenando esquema y nombre de tabla si hay esquema
  (`GetUndelimitedTableName()`).
- **Bloqueo de migraciones (`AcquireDatabaseLock`) es un no-op deliberado**: SQL Server usa
  `sp_getapplock` para evitar que dos procesos apliquen migraciones al mismo tiempo; no se encontró
  ni se verificó un equivalente en ASE. Se decidió no simular un lock que no protege de verdad contra
  concurrencia real, y devolver un `IMigrationsDatabaseLock` no-op (`LockReleaseBehavior.Connection`)
  en vez de inventar sintaxis no verificada.

### Bug de discovery de migraciones — no específico de ASE, pero bloqueó todo el testing

Durante el armado del test end-to-end, `Database.GetMigrations()` devolvía siempre una colección
vacía a pesar de que la clase de migración (`InitialMigration`) existía en el assembly, tenía
`[Migration("...")]`, no era abstracta ni genérica, y tenía constructor sin parámetros — reflexión
pura (`Assembly.GetTypes()`) sí la encontraba. La causa, confirmada leyendo el código fuente real de
`MigrationsAssembly.Migrations` en `dotnet/efcore` (tag `v9.0.17`): el filtro de esa propiedad exige
`t.GetCustomAttribute<DbContextAttribute>()?.ContextType == _contextType` — es decir, **toda clase de
migración necesita el atributo `[DbContext(typeof(TuContext))]`**, aunque solo haya un `DbContext` en
el assembly. Sin él, `GetCustomAttribute<DbContextAttribute>()` devuelve `null`, `null?.ContextType`
nunca es igual al tipo de contexto real, y la migración se descarta en silencio (sin warning, sin
excepción). Esto no es una particularidad de ASE — afecta a cualquier provider — pero no está
mencionado con claridad en la documentación pública de EF Core para migraciones definidas a mano
fuera del flujo normal de `dotnet ef migrations add`. Nota para uso futuro del provider: cualquier
migración escrita a mano (no generada por las herramientas de EF Core, que sí agregan el atributo
automáticamente) necesita `[DbContext(typeof(TuDbContext))]` explícito.

Nota aparte: `Microsoft.EntityFrameworkCore.DbContextAttribute` vive en el namespace
`Microsoft.EntityFrameworkCore.Infrastructure`, no en `Microsoft.EntityFrameworkCore` — y como
`Microsoft.EntityFrameworkCore.DbContext` (la clase) ya está en scope por el `using` de ese
namespace, escribir `[DbContext(...)]` a secas puede generar el error de compilación "'DbContext' no
es una clase de atributos" en vez de resolverse a `DbContextAttribute` como el lector esperaría; hay
que usar `[DbContextAttribute(...)]` explícito o el nombre completo.

### Hallazgo de pooling — `AdoNetCore.AseClient` no libera conexiones de forma confiable

Incluso después de arreglar el bug de `OBJECT_ID`, el test end-to-end seguía fallando en su
`Dispose()`: `EnsureDeleted()` tiraba "Cannot drop or replace the database ... because it is
currently in use", incluso con el retry de `ClearPools()` llevado a 15 intentos de 500ms (~7.5
segundos en total). Se probó consultar `master..sysprocesses` filtrando por `dbid = DB_ID(...)`
buscando una sesión colgada para matarla con `KILL`, pero para cuando se corrió la consulta (unos
minutos después, mientras se armaba el script de prueba) la tabla ya no mostraba ninguna sesión — el
problema no es una sesión servidor colgada de forma permanente, sino que **el pool de conexiones del
lado del driver (.NET) no cierra la conexión física al llamar `Close()`/`Dispose()`**, y
`AseConnection.ClearPools()` no fuerza ese cierre de forma confiable ni rápida en la versión 0.19.2.

**Fix verificado**: agregar `Pooling=false` al connection string. Con esto, `Close()` cierra la
conexión física de verdad y el `DROP DATABASE` en `Dispose()` funciona al primer intento, sin
necesidad de ningún retry. Se aplicó en el connection string del test
(`MigrationEndToEndTests.ConnectionString`) — **no** se cambió el comportamiento default del
provider, porque este problema solo importa en escenarios de crear/borrar la base repetidamente
(tests, herramientas de scaffolding); una aplicación real que no dropea su propia base de datos en
caliente no lo sufre, y desactivar pooling por default penalizaría el rendimiento normal de conexión.
El retry de `ClearPools()` en `AseDatabaseCreator.Delete()` se dejó como red de seguridad para
consumidores que no puedan desactivar pooling, pero queda documentado que no es una solución
completa.

### Tests

`test/EntityFrameworkCore.Ase.FunctionalTests/Migrations/MigrationEndToEndTests.cs` — test de
integración real (no mocks) que ejercita `AseDatabaseCreator`, `AseMigrationsSqlGenerator` y
`AseHistoryRepository` juntos: crea la base, migra, inserta una fila usando la columna `IDENTITY`
generada automáticamente, verifica el valor devuelto, confirma que el historial de migraciones quedó
registrado, y borra todo en `Dispose()`. Pasa contra la instancia real de ASE (puerto 5000).

### Pendiente / fuera de alcance de esta fase

- No se probó el flujo completo de `SaveChanges()` (INSERT/UPDATE/DELETE vía el pipeline normal de
  EF Core, no `ExecuteSqlRaw`) — las clases `AseUpdateSqlGenerator` y
  `AseModificationCommandBatchFactory` de la Fase 4 siguen sin verificación real de extremo a
  extremo. Queda para una fase posterior o para cuando se agregue un test específico.
- No se probaron migraciones que hagan `AddColumn`/`DropColumn`/`AlterColumn`/`CreateIndex` sobre una
  tabla ya poblada — solo `CreateTable`/`DropTable` (usado en el `Down()` de la migración de prueba,
  pero no ejercitado por el test, que no llama a `Database.Migrate()` hacia atrás).
- El límite de espacio en disco de la instancia de desarrollo (dispositivo por defecto muy chico, se
  vieron errores de "at least 6 megabytes" al acumular bases de prueba sin borrar) es una limitación
  del entorno, no del provider — mencionado acá porque obligó a limpiar bases de prueba manualmente
  varias veces durante esta fase.

### Scaffolding (`dotnet ef dbcontext scaffold`)

Pedido explícitamente por el usuario antes de la Fase 7 ("antes de la fase 7, quiero que podamos hacer
scaffold"). No formaba parte de ninguna de las 8 fases originales del plan (`CLAUDE.md`: la Fase 0 solo
investigó qué catálogos usar, sin un paso de implementación dedicado). Se implementó y **se verificó
corriendo la CLI real de `dotnet-ef`** (`dotnet ef dbcontext scaffold "..." EntityFrameworkCore.Ase -o
Models`) contra una base ASE real con dos tablas relacionadas por FK, dos índices y una columna
`IDENTITY` — no solo con tests.

#### Piezas agregadas

- `Microsoft.EntityFrameworkCore.Design` como paquete nuevo (`PrivateAssets="all"`, es una dependencia
  de diseño, no de runtime).
- `AseDatabaseModelFactory : DatabaseModelFactory` (`Scaffolding/Internal/`) — lee el esquema real de
  ASE. Ver detalle de catálogos más abajo.
- `AseCodeGenerator : ProviderCodeGenerator` (`Design/Internal/`) — genera la llamada
  `optionsBuilder.UseAse("...")` en el `OnConfiguring` scaffoldeado.
- `AseDesignTimeServices : IDesignTimeServices` (`Design/`) — registra las dos piezas de arriba, más
  `AddEntityFrameworkAse()` completo (el host de `dotnet-ef` arma su propio `IServiceProvider`
  aislado solo para scaffolding, que necesita el stack completo del provider — confirmado que sin
  `AddEntityFrameworkAse()` ahí, falla al no poder resolver `IRelationalTypeMappingSource`) y
  `EntityFrameworkRelationalServicesBuilder`/`EntityFrameworkRelationalDesignServicesBuilder`
  (`TryAddCoreServices()`, necesario para que puedan resolverse `ProviderCodeGeneratorDependencies` y
  el resto de las dependencias de diseño genéricas de EF Core).
- `[assembly: DesignTimeProviderServices("EntityFrameworkCore.Ase.Design.AseDesignTimeServices")]`
  en `AssemblyInfo.cs` — **obligatorio**, no opcional: corriendo `dotnet-ef` real se confirmó que sin
  este atributo de assembly, las herramientas no encuentran `AseDesignTimeServices` aunque sea
  pública ("Unable to find expected assembly attribute [DesignTimeProviderServices]"). La suposición
  inicial de que alcanzaba con que la clase fuera pública y estuviera en el assembly del provider era
  incorrecta.

#### Catálogos de ASE usados (todos verificados contra la instancia real)

- **Tablas**: `SELECT name FROM sysobjects WHERE type = 'U'` (mismo catálogo que
  `AseDatabaseCreator.HasTables()` desde la Fase 5). Se excluye `__EFMigrationsHistory` del resultado,
  igual que hacen otros providers.
- **Columnas**: se combinan dos fuentes porque ninguna alcanza sola:
  - `sp_columns @table_name` (procedimiento catálogo estándar de Sybase/ASE, equivalente a
    `SQLColumns` de ODBC) da longitud/precisión/escala/nullable de forma confiable.
  - Pero para una columna `IDENTITY`, `sp_columns` devuelve el nombre de tipo genérico "numeric
    identity" en vez del tipo realmente declarado (`int`, `smallint`, etc.) — hay que resolverlo aparte
    con `syscolumns` joineada con `systypes` (`JOIN systypes t ON c.usertype = t.usertype`), que da el
    nombre de tipo real y el bit de `IDENTITY` (`c.status & 0x80`, confirmado contra la instancia real
    con columnas `int`/`smallint`/`bigint IDENTITY`: el bit 128 de `status` está prendido solo en la
    columna identity).
- **Primary key**: `sp_pkeys @table_name` (equivalente a `SQLPrimaryKeys` de ODBC), da las columnas en
  orden vía `key_seq`.
- **Índices**: `sp_helpindex @objname`. Devuelve `index_keys` como un string separado por comas (no una
  fila por columna) y una descripción de texto libre (`index_description`) que incluye la palabra
  "unique" cuando corresponde — hay que parsear ambas cosas a mano, no hay columnas booleanas
  dedicadas. Tira un `AseException` (no un resultset vacío) cuando la tabla no tiene ningún índice.
  Se excluye del resultado el índice que coincide exactamente con las columnas de la primary key (ASE
  crea uno automáticamente para respaldarla) para no duplicarlo con `DatabaseTable.PrimaryKey`.
- **Foreign keys**: `sp_fkeys @fktable_name = @fktable_name` (equivalente a `SQLForeignKeys` de ODBC).
  **Diferencia real encontrada con el estándar ODBC**: la versión de ASE de este procedimiento no
  devuelve columnas de nombre de constraint (`fk_name`/`pk_name`) — confirmado contra la instancia
  real. Sin nombre para agrupar filas de una FK compuesta, se usa el reinicio de `key_seq` a 1 como
  señal de "arranca una FK nueva" (verificado con un caso de dos FKs simples distintas hacia la misma
  tabla principal, y otro de una FK compuesta de dos columnas: el reinicio de `key_seq` distingue
  ambos casos correctamente). El nombre real de la constraint existe en `sysobjects` (`type = 'RI'`)
  pero identificar cuál corresponde a cada grupo de filas requeriría además joinear `sysreferences` —
  no se hizo por alcance/tiempo; en cambio se sintetiza `FK_{tabla}_{principal}_{n}`, que no afecta
  las relaciones ni columnas generadas, solo el nombre explícito de la constraint en el código
  scaffoldeado.
- **Referential actions**: se probó explícitamente contra ASE real que ni siquiera es *posible*
  declarar `ON DELETE/UPDATE CASCADE` o `SET NULL` en la sintaxis de `FOREIGN KEY` de ASE ("Incorrect
  syntax near the keyword 'ON'") — así que toda FK real en una base ASE es, en la práctica, siempre
  `ReferentialAction.NoAction`. No hace falta interpretar `update_rule`/`delete_rule` de `sp_fkeys`.
- **Llamado a stored procedures con parámetros**: los placeholders posicionales `?` (que sí funcionan
  para `SELECT`s parametrizados normales en este driver) **no** funcionan para llamar a un
  stored procedure ni siquiera con `CommandType.Text` — tira "Incorrect syntax near '?'". Hace falta
  usar parámetros con nombre real (`@table_name`, etc.), tanto en el texto del comando como en
  `Parameter.ParameterName`; confirmado que esto funciona tanto con `CommandType.Text` como con
  `CommandType.StoredProcedure`.
- **Charset cp850**: el mismo problema de la Fase 4 (`AseRelationalConnection`) aparece de nuevo acá,
  porque `AseDatabaseModelFactory` abre su propia `AseConnection` sin pasar por
  `AseRelationalConnection` — el host de `dotnet-ef` es un proceso separado que nunca ejecuta ese
  constructor estático. Se repitió el mismo `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`
  en el constructor estático de `AseDatabaseModelFactory`.

#### Bug real encontrado después de publicar 0.1.0: `GetIndexes` explota contra tablas sin columnas indexables

Al validar manualmente cómo scaffoldear con el paquete recién publicado, se probó (sin querer, de
prueba) contra la base `master`, que además de tablas normales tiene decenas de pseudo-tablas de
monitoreo (`monProcess`, `monSysWaits`, etc. — vistas en memoria de estadísticas del server, no
tablas reales, pero igual aparecen con `type = 'U'` en `sysobjects`). Contra esas tablas,
`GetIndexes()` explotaba con `System.ArgumentException: Value does not fall within the expected
range.` en `AseDataReader.GetOrdinal`.

Causa: ya se sabía que `sp_helpindex` tira un `AseException` cuando una tabla común no tiene ningún
índice (cubierto con un `catch`), pero para estas pseudo-tablas de monitoreo hace algo distinto —
**no tira excepción**, devuelve un resultset que se ejecuta sin error pero con **cero columnas**
(`reader.FieldCount == 0`). El contrato estándar de ADO.NET para `IDataRecord.GetOrdinal` con un
nombre de columna inexistente es devolver `-1`, pero `AdoNetCore.AseClient` en cambio tira
`ArgumentException` — no había cobertura para ese caso. Fix: chequear `reader.FieldCount == 0`
explícitamente antes de llamar a `GetOrdinal`, y tratarlo igual que el caso de la excepción (sin
índices para scaffoldear).

Este bug ya estaba en el paquete `0.1.0` publicado a nuget.org — nunca se había probado el scaffold
contra `master` durante el desarrollo original de la feature (los tests y la verificación manual
previa siempre usaron bases de aplicación normales, sin pseudo-tablas). No afecta a bases de
aplicación típicas (sin `mon*`), pero sí a cualquier intento de scaffoldear `master` directamente, y
en teoría a cualquier otra tabla real que por algún motivo produzca el mismo resultset sin columnas.
Corregido en el código fuente; pendiente de decidir si amerita una versión `0.1.1` en nuget.org (no
se publicó todavía al momento de escribir esto).

#### No modelado / fuera de alcance

- **Schema/owner de ASE**: igual que en fases anteriores, este provider no modela el concepto de
  "owner" de objeto de ASE — todas las tablas scaffoldeadas quedan con `Schema = null`. No se pidió
  soporte multi-schema y no hay evidencia de que haga falta para el caso de uso actual.
- **Valores default de columna**: no se scaffoldean (`DefaultValueSql` queda sin setear). Recuperar el
  texto de un default en ASE requiere resolver el objeto por `syscolumns.cdefault` y leer
  `syscomments.text` para ese objeto — no se hizo por complejidad/tiempo frente al beneficio (afecta
  solo la fidelidad del scaffold, no la usabilidad del modelo resultante).
- **Nombre real de constraint de FK**: ver arriba (sintetizado en vez de leído de `sysobjects`/`type='RI'`).
- Tipos no cubiertos por `AseTypeMappingSource` (ej. `money`, ya que la Fase 3 no lo agregó): el
  factory los reporta igual con su `StoreType` real (ej. `"money"`); si el mapping source no lo
  reconoce, el pipeline genérico de scaffolding de EF Core simplemente no puede mapearlos a un tipo
  CLR y los reporta como columna sin soporte — comportamiento estándar de EF Core, no un bug de este
  provider.

#### Tests

- `test/EntityFrameworkCore.Ase.FunctionalTests/Scaffolding/AseDatabaseModelFactoryTests.cs` (real,
  contra ASE): arma un esquema de dos tablas con FK/índices/IDENTITY por SQL crudo (sin pasar por este
  provider en absoluto) y confirma que `AseDatabaseModelFactory.Create(...)` lo lee de vuelta
  correctamente — tablas, columnas (tipo/nullable/longitud/precisión/escala/identity), primary key,
  el filtro de exclusión del índice de la PK, índice único y no-único, y la foreign key con su tabla y
  columnas principales. Un segundo test confirma que el filtro `options.Tables` excluye
  correctamente FKs hacia tablas que quedaron fuera del scaffold.
- Verificación manual adicional (no automatizada, no queda como test permanente): corrida real de
  `dotnet ef dbcontext scaffold` con la CLI de `dotnet-ef` contra una base con el mismo esquema,
  confirmando que el código C# generado (`DbContext` + entidades) compila y refleja correctamente
  tipos, nullability, precisión/escala, PK, FK con navegación y ambos índices.

## Fase 6 — Metadata / Annotations

La Fase 6 es opcional según el plan original: "solo si ASE tiene features específicas que valga la
pena exponer por fluent API". Se encontró una: el esquema de locking de tabla (`LOCK
ALLPAGES`/`DATAPAGES`/`DATAROWS`), que no tiene equivalente en SQL Server — confirmado contra ASE
real que las tres variantes son válidas como cláusula final de `CREATE TABLE`.

### Arquitectura de anotaciones (API actual de EF Core, no el patrón viejo de `IEntityTypeAnnotations`)

Como advierte el `CLAUDE.md` del proyecto, el patrón de `SqlServerEntityTypeAnnotations` de los posts
de Arthur Vickers (EF Core 1.x) ya no existe. La arquitectura actual, confirmada leyendo el código
fuente real de `dotnet/efcore` (tag `v9.0.17`):

- Las anotaciones se guardan como metadata plana (`IAnnotatable.SetAnnotation`/`this[name]`) —
  no hace falta ninguna clase wrapper tipo "Annotations".
- `IRelationalAnnotationProvider` (`RelationalAnnotationProvider` como base) es el servicio que decide
  qué anotaciones de propiedades/entidades del modelo terminan expuestas en los objetos del modelo
  relacional en tiempo de ejecución (`ITable`, `IColumn`, etc.) — por default no expone ninguna,
  cada provider tiene que overridear `For(ITable, bool designTime)` / `For(IColumn, bool designTime)`
  explícitamente para las suyas.
- `MigrationsModelDiffer` arma `CreateTableOperation`/`ColumnOperation` copiando directamente
  `target.GetAnnotations()` de esos objetos (`ITable`/`IColumn`) — confirmado leyendo
  `MigrationsModelDiffer.Add(ITable, ...)` en el código fuente real. Por eso `IRelationalAnnotationProvider`
  es el único servicio que hace falta para que una anotación de modelo llegue hasta el SQL generado
  por las migraciones; no existe (ni hace falta) una clase separada de "migrations annotation
  provider" para este caso.

Se agregó `AseAnnotationNames.LockScheme` y el enum `AseLockScheme { AllPages, DataPages, DataRows }`,
más el fluent API `ForAseUseLockScheme(...)` sobre `EntityTypeBuilder` (con overload genérico), tal
como pide el `CLAUDE.md` para esta fase. `AseAnnotationProvider : RelationalAnnotationProvider` (nueva
clase) proyecta esa anotación desde la entidad hacia la tabla, y `AseMigrationsSqlGenerator` fue
extendido para overridear `Generate(CreateTableOperation, ...)`: genera el `CREATE TABLE` normal
(vía `base.Generate(..., terminate: false)`) y agrega `LOCK {ESQUEMA}` antes del terminador si la
anotación está presente.

### Bug real encontrado al testear de punta a punta: el IDENTITY automático de la Fase 5 nunca llegaba a las migraciones

Al escribir el primer test de esta fase que usa `EnsureCreated()` con convenciones automáticas (en vez
de una migración escrita a mano como en la Fase 5), apareció "The column Id in table Gadgets does not
allow null values" al insertar sin especificar `Id` — la columna se había creado como `int NOT NULL`
normal, no como `IDENTITY`. Causa: `AseValueGenerationStrategyConvention` (Fase 5) marca la anotación
`ValueGenerationStrategy` sobre la **propiedad** del modelo, pero nada proyectaba esa anotación desde
la propiedad hacia la **columna** del modelo relacional — el único motivo por el que el test de la
Fase 5 pasaba es que esa migración estaba escrita a mano con `.Annotation(...)` puesto directamente en
el `ColumnOperation`, sin pasar nunca por este camino. Es decir: **el IDENTITY automático (sin migrar
a mano) nunca había funcionado de punta a punta hasta ahora**. Fix: se agregó
`AseAnnotationProvider.For(IColumn, bool designTime)`, que recorre `column.PropertyMappings` y
proyecta la anotación de la propiedad hacia la columna, igual que se hizo para `LockScheme` a nivel
tabla.

### Hallazgo de test flake: `CREATE DATABASE` concurrente contra `model`

Al agregar el segundo test funcional que crea su propia base (el de esta fase, además del de la Fase
5), correr la suite completa empezó a fallar intermitentemente con "The model database is unavailable.
It is being used to create a new database." — ASE clona la base `model` como plantilla en cada
`CREATE DATABASE`, y dos `CREATE DATABASE` concurrentes contra la misma instancia chocan ahí. xUnit
corre clases de test de distintas collections en paralelo por default. Fix: se agregó
`[assembly: CollectionBehavior(DisableTestParallelization = true)]` en
`test/EntityFrameworkCore.Ase.FunctionalTests/AssemblyInfo.cs` — todos los tests de ese assembly pegan
contra la misma instancia real compartida, así que tiene sentido serializarlos siempre, no solo para
este caso puntual.

### Tests

- `test/EntityFrameworkCore.Ase.Tests/Migrations/AseMigrationsSqlGeneratorTests.cs` (unitario, sin
  ASE real): confirma que `ForAseUseLockScheme` deja la anotación en el `CreateTableOperation`
  generado por el diff del modelo, y que el SQL generado contiene `LOCK DATAROWS`.
- `test/EntityFrameworkCore.Ase.FunctionalTests/Migrations/LockSchemeEndToEndTests.cs` (real, contra
  ASE): `EnsureCreated()` con una entidad configurada con `ForAseUseLockScheme(DataRows)`, confirma
  que ASE acepta el `CREATE TABLE ... LOCK DATAROWS` real y que la tabla funciona normalmente después
  (insert + query). De paso, al no fallar más por el `Id` en `NULL`, esto también sirve como
  confirmación de punta a punta de que el fix del IDENTITY automático (párrafo anterior) funciona.

### Pendiente / fuera de alcance de esta fase

- Solo se cubrió `LockScheme` a nivel tabla. No se investigaron otras features específicas de ASE que
  podrían valer la pena exponer más adelante (segmentos/devices para ubicación física de datos,
  `IDENTITY_GAP`, particionado) — no se pidieron explícitamente y hubiera requerido verificar sintaxis
  adicional contra la instancia real sin un objetivo concreto todavía.

## Fase 7 — Naming del paquete NuGet

Pedido explícito del usuario: `PackageId` = `Chiola.EntityFrameworkCore.Ase`, siguiendo la convención
`<Empresa/Proyecto>.EntityFrameworkCore.<Motor>` sugerida por Microsoft (Fase 7 del `CLAUDE.md`). Solo
se cambió el `PackageId` — el nombre del ensamblado, el namespace raíz (`EntityFrameworkCore.Ase`) y la
carpeta del proyecto se dejaron como están, porque no se pidió renombrarlos y hacerlo hubiera implicado
tocar todos los archivos del repo sin necesidad real (`PackageId` distinto de `AssemblyName` es un
escenario perfectamente soportado por NuGet).

Metadata de paquete agregada al `.csproj` (confirmada por el usuario ante la falta de un `LICENSE`/repo
remoto previos, en vez de asumirla):

- `Version` = `0.1.0` (primera versión "empaquetable", no publicada todavía).
- `Authors` = Fernando Chiola.
- `Description`: "Entity Framework Core provider for SAP ASE (Sybase Adaptive Server Enterprise)."
- `PackageLicenseExpression` = `MIT` — se agregó `LICENSE` (MIT) en la raíz del proyecto, no existía
  antes.
- `RepositoryUrl` = `https://github.com/ferchiola/EntityFrameworkCore.Ase` (dato del usuario — no se
  verificó que el repo remoto exista o tenga contenido, ya que el proyecto local ni siquiera tiene
  `git init` todavía en este momento).
- `PackageTags`: `entity-framework-core;sap-ase;sybase;ado.net` (tal cual los sugeridos en el
  `CLAUDE.md`).
- `README.md` y `LICENSE` empaquetados vía `PackagePath=""` (`PackageReadmeFile` apunta al primero) —
  sin esto, `dotnet pack` advertía por la falta de README.

Verificado con `dotnet pack -c Release`: genera
`bin/Release/Chiola.EntityFrameworkCore.Ase.0.1.0.nupkg` sin errores ni warnings, y los 43 tests
(31 unitarios + 12 funcionales contra ASE real) siguen en verde después del cambio.

### Pendiente / fuera de alcance de esta fase

- No se hizo `dotnet nuget push` ni se publicó el paquete a ningún feed (ni nuget.org ni uno privado)
  — no se pidió, y de todos modos el repo remoto de GitHub declarado en `RepositoryUrl` todavía no
  tiene el código subido.
- No se agregó ningún ícono de paquete (`PackageIcon`) ni `PackageProjectUrl` explícito (por default
  NuGet.org puede llegar a usar `RepositoryUrl` como referencia, pero no son estrictamente lo mismo) —
  no se pidió y no había un valor claro para ninguno de los dos.
