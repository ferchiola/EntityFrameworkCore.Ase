## Contexto que le das a Claude Code

```
Quiero crear desde cero un proveedor de Entity Framework Core para SAP ASE
(Adaptive Server Enterprise, ex-Sybase). El paquete se debe llamar algo así
como `EntityFrameworkCore.Ase`.

Como referencia de arquitectura y buenas prácticas, seguí estos lineamientos:

1. La guía oficial de Microsoft sobre cómo escribir un provider:
   https://learn.microsoft.com/en-us/ef/core/providers/writing-a-provider
   Esta guía es muy breve y remite a una serie de posts de Arthur Vickers
   ("So you want to write an EF Core provider...") que son de EF Core 1.1
   (2016). ESA API YA CAMBIÓ MUCHO. No la copies literal: usala solo para
   entender los "bloques" conceptuales (DI de servicios, options extension,
   metadata/annotations, migraciones, tests de especificación), pero para el
   código real basate en la arquitectura ACTUAL de EF Core (10/9), tomando
   como referencia el código fuente abierto de:
   - Provider oficial de SQL Server: https://github.com/dotnet/efcore
     (carpeta src/EFCore.SqlServer)
   - Provider oficial de SQLite: https://github.com/dotnet/efcore
     (carpeta src/EFCore.Sqlite.Core)
   - Npgsql (PostgreSQL, muy usado como referencia de "third party provider"
     relacional): https://github.com/npgsql/Npgsql.EntityFrameworkCore.PostgreSQL
   - Pomelo (MySQL): https://github.com/PomeloFoundation/Pomelo.EntityFrameworkCore.MySql

2. Como es un motor relacional (SAP ASE es SQL/T-SQL like), el provider debe
   heredar de las clases base "Relational*" de EF Core
   (RelationalOptionsExtension, RelationalDatabaseProviderServices /
   EntityFrameworkRelationalServicesBuilder, RelationalConnection,
   RelationalTypeMappingSource, QuerySqlGenerator relacional, etc.), no de
   las clases base "no relacionales".

3. La conectividad a la base la vamos a hacer sobre el driver ADO.NET oficial
   de SAP para ASE (AseClient / Sybase.AdoNet4.AseClient, o el driver que
   definamos juntos si hay alguno más moderno/soportado). Confirmame con el
   usuario cuál driver ADO.NET vamos a usar antes de generar el código de
   conexión.

Quiero que trabajes en fases, mostrándome el resultado de cada fase antes de
avanzar a la siguiente, y que documentes decisiones de diseño (sobre todo
las que dependan de particularidades de ASE) en un archivo DECISIONS.md.
```

---

## Fase 0 — Investigación y setup del repo

```
Antes de escribir código:

1. Investigá y resumime las particularidades de SAP ASE relevantes para un
   provider de EF Core, comparado con SQL Server (que es el motor más
   parecido). Necesito que cubras al menos:
   - Sintaxis de paginación (ASE no tiene OFFSET/FETCH nativo en todas las
     versiones; investigar SET ROWCOUNT, TOP, o alternativas)
   - Manejo de identity / autoincrement (IDENTITY column, @@identity vs
     scope_identity equivalente en ASE)
   - Delimitadores de identificadores (¿usa corchetes como SQL Server,
     comillas dobles, o backticks?)
   - Tipos de datos ASE y su mapeo a tipos .NET/CLR (int, bigint, varchar,
     nvarchar, text, image, datetime, bigdatetime, money, decimal, etc.)
   - Concurrencia optimista: ¿existe rowversion/timestamp en ASE?
   - Transacciones e isolation levels soportados
   - Cómo se generan/consultan metadatos de esquema (para scaffolding /
     reverse engineering, sys tables o catálogos de ASE)
   - Límites conocidos (longitud máxima de identificadores, de queries, etc.)

2. Creá la estructura de solución en .NET (carpeta `src`, `test`, `.sln`):
   - `src/EntityFrameworkCore.Ase/` → el provider en sí
   - `test/EntityFrameworkCore.Ase.Tests/` → tests unitarios propios
   - `test/EntityFrameworkCore.Ase.FunctionalTests/` → para los
     specification tests de EF Core más adelante
   - README.md inicial y DECISIONS.md vacío para ir registrando decisiones

3. Agregá el paquete NuGet base `Microsoft.EntityFrameworkCore.Relational`
   con la versión que definamos, y el driver ADO.NET de ASE que hayamos
   confirmado.
```

---

## Fase 1 — Registro de servicios (DI) y "Use..." method

```
Implementá el esqueleto de integración con el sistema de DI de EF Core:

1. `AseOptionsExtension : RelationalOptionsExtension`
   - Info() debe exponer un DbContextOptionsExtensionInfo propio
   - ApplyServices(IServiceCollection) debe registrar los servicios del
     provider vía EntityFrameworkRelationalServicesBuilder
   - Constructor de copia para inmutabilidad

2. `AseDbContextOptionsBuilder` (análoga a SqlServerDbContextOptionsBuilder)
   para configuración fluida específica del provider (ej: nivel de
   compatibilidad ASE, comportamiento de paginación, etc. — a definir)

3. Extension method `UseAse(this DbContextOptionsBuilder, string
   connectionString, Action<AseDbContextOptionsBuilder> aseOptionsAction =
   null)` con overload genérico `UseAse<TContext>`.

4. `AseServiceCollectionExtensions.AddEntityFrameworkAse(this
   IServiceCollection)`:
   - Registra IDatabaseProvider vía
     services.AddRelational() equivalente actual
     (EntityFrameworkRelationalServicesBuilder)
   - TryAdd de todos los servicios core que vamos a ir sumando en las
     fases siguientes (dejalos con un TODO si aún no existen)

Mostrame el resultado y explicame en DECISIONS.md por qué elegiste cada
lifetime (singleton/scoped) para los servicios registrados.
```

---

## Fase 2 — Conexión

```
Implementá:

1. `AseRelationalConnection : RelationalConnection` que use el driver
   ADO.NET de ASE para crear las DbConnection concretas.
2. Manejo correcto de:
   - Apertura/cierre de conexión async y sync
   - Cadena de conexión (parseo básico o delegación al driver)
   - Soporte de connection pooling si el driver lo permite
3. Tests unitarios básicos de esta pieza (con mocks, sin necesidad de una
   ASE real todavía).
```

---

## Fase 3 — Type Mapping

```
Implementá `AseTypeMappingSource : RelationalTypeMappingSource`:

1. Mapeo completo de tipos CLR ↔ tipos ASE (usá la investigación de la
   Fase 0). Cubrí como mínimo: bool, byte, short, int, long, decimal,
   double, float, DateTime, DateTimeOffset (si ASE lo soporta o hay que
   simular), Guid (uniqueidentifier equivalente o binary), string
   (varchar/nvarchar/text con límites de longitud), byte[]
   (binary/varbinary/image).
2. Mapeos explícitos para casos especiales (rowversion/concurrency token
   si existe equivalente, o documentá en DECISIONS.md si no hay soporte
   nativo y cómo lo vamos a resolver: shadow property + trigger, etc.)
3. Tests unitarios de mapeo tipo por tipo.
```

---

## Fase 4 — SQL Generation (queries)

```
Implementá el pipeline de generación de SQL para queries LINQ:

1. `AseSqlGenerationHelper : RelationalSqlGenerationHelper` — delimitadores
   de identificadores propios de ASE, formato de literales, terminador de
   sentencias, etc.
2. `AseQuerySqlGenerator` (o el nombre equivalente en la versión actual del
   pipeline de queries, revisá cómo lo llama SqlServerQuerySqlGenerator hoy)
   — con especial atención a:
   - Paginación (Skip/Take) usando la estrategia que definiste en Fase 0
   - Funciones de fecha/string específicas de ASE si hace falta traducirlas
   - Cualquier construcción SQL que ASE no soporte igual que T-SQL estándar
3. Un `AseQueryTranslationPostprocessor` / miembros de traducción de
   métodos LINQ a funciones ASE si aplica (ej: string.Contains,
   DateTime.AddDays, etc.) — priorizá lo mínimo para que las queries
   básicas (Where, OrderBy, Skip/Take, joins simples) funcionen antes de
   cubrir casos avanzados.
4. Documentá en DECISIONS.md cualquier limitación conocida (ej: "no se
   soporta tal traducción porque ASE no tiene una función equivalente").
```

---

## Fase 5 — Migraciones y Database Creator

```
Implementá:

1. `AseMigrationsSqlGenerator : MigrationsSqlGenerator` — DDL para
   CreateTable, AddColumn, CreateIndex, etc. en sintaxis ASE.
2. `AseDatabaseCreator : RelationalDatabaseCreator` — crear/eliminar
   base de datos, verificar existencia.
3. `AseHistoryRepository : HistoryRepository` — tabla de historial de
   migraciones (__EFMigrationsHistory) adaptada a ASE.
4. Probá con una migración simple end-to-end (CreateTable de una entidad
   de ejemplo) contra una instancia real o dockerizada de ASE si está
   disponible; si no lo está, dejá el test marcado como
   Skip("requiere instancia ASE") con instrucciones claras de cómo
   habilitarlo.
```

---

## Fase 6 — Metadata / Annotations (opcional, según lo que necesite ASE)

```
Solo si ASE tiene features específicas que valga la pena exponer por fluent
API (por ejemplo, algún tipo de tabla particionada, opciones de
almacenamiento, etc.):

1. Definí `AseAnnotationNames` con el prefijo del provider.
2. Creá las clases de anotaciones (IAseXxxAnnotations /
   AseXxxAnnotations) siguiendo el patrón de SqlServerEntityTypeAnnotations
   pero adaptado a la API actual de anotaciones de EF Core (revisá cómo lo
   hace hoy Npgsql, que es el ejemplo más mantenido de "extensiones fluent
   de un provider third-party").
3. Fluent API `ForAseXxx(...)` con overloads genéricos.

Si no hay nada específico que exponer todavía, saltá esta fase y anotalo en
DECISIONS.md para revisarlo más adelante.
```

---

## Fase 7 — Naming del paquete NuGet

```
Seguí la convención sugerida por Microsoft:
`<Empresa/Proyecto>.EntityFrameworkCore.<Motor>`

Propongo `EntityFrameworkCore.Ase` o `<TuOrg>.EntityFrameworkCore.Ase`.
Armá el .nuspec / csproj con metadata de paquete (autor, descripción,
licencia, repo, tags: "entity-framework-core", "sap-ase", "sybase", "ado.net").
```

---

## Fase 8 — Testing con el Specification Test Suite de EF Core

```
1. Agregá al proyecto de FunctionalTests el paquete
   Microsoft.EntityFrameworkCore.Relational.Specification.Tests (buscá la
   versión más reciente disponible que sea compatible con la versión de
   EF Core que estamos targeteando).
2. Empezá por extender NorthwindWhereQueryRelationalTestBase (es el punto
   de partida recomendado por la doc de Microsoft). Para eso vas a
   necesitar:
   - AseNorthwindQueryFixture (o el nombre equivalente actual)
   - AseNorthwindTestStoreFactory
   - Script Northwind.sql adaptado a sintaxis ASE para poblar la DB de test
3. Corré los tests, reportame cuáles pasan y cuáles no, con el detalle del
   motivo de falla de cada uno.
4. Para los que fallan por diferencias reales de SQL esperado, usá
   AssertSql con el ITestOutputHelper para inspeccionar el SQL generado
   real vs. el esperado, y ajustá el QuerySqlGenerator o creá el baseline
   correcto.
5. Cuando tengamos una base sólida de tests en verde, creá
   AseComplianceTest extendiendo RelationalComplianceTestBase, y
   reportame qué test classes faltan por implementar todavía.
```

---

## Reglas generales para todas las fases

```
- Priorizá que compile y tenga tests antes de avanzar a la siguiente fase.
- Si en algún punto una decisión depende de saber exactamente qué versión
  de SAP ASE vamos a soportar (16.x, versiones anteriores, etc.) o qué
  driver ADO.NET vamos a usar, PARÁ y preguntame antes de asumir.
- No inventes sintaxis SQL de ASE que no puedas verificar: si tenés dudas
  sobre un comportamiento específico del motor, decímelo explícitamente en
  vez de asumir que se comporta como SQL Server.
- Mantené DECISIONS.md actualizado en cada fase con: qué se decidió, por
  qué, y qué alternativas se descartaron.
- Al final de cada fase, dame un resumen corto de qué quedó hecho, qué
  quedó pendiente/con TODO, y qué necesitás de mí para seguir.
```