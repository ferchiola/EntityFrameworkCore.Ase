# EntityFrameworkCore.Ase

Proveedor de Entity Framework Core para SAP ASE (Adaptive Server
Enterprise, ex-Sybase), escrito desde cero siguiendo la arquitectura
relacional actual de EF Core (9.x).

## Alcance

- Motor objetivo: **SAP ASE 16.x**.
- Framework: **.NET 9 / EF Core 9** (`Microsoft.EntityFrameworkCore.Relational`).
- Driver ADO.NET: **[AdoNetCore.AseClient](https://github.com/DataAction/AdoNetCore.AseClient)**
  (Apache-2.0, implementación pura en C# del protocolo TDS 5.0 — no depende
  de COM ni de librerías nativas de SAP, corre en Windows/Linux/Docker).

## Estructura

```
EntityFrameworkCore.Ase/
├── src/EntityFrameworkCore.Ase/              # El provider
├── test/EntityFrameworkCore.Ase.Tests/       # Tests unitarios propios
├── test/EntityFrameworkCore.Ase.FunctionalTests/  # Specification tests de EF Core
├── DECISIONS.md                               # Decisiones de diseño, fase por fase
└── EntityFrameworkCore.Ase.sln
```

## Estado

Fases 0 a 8 completas (investigación/setup, DI, conexión, type mapping,
queries, migraciones, metadata/annotations, scaffolding, naming del
paquete NuGet — publicado como `Chiola.EntityFrameworkCore.Ase`). En
Fase 9 (Specification Test Suite de EF Core), la fase final. Ver
`DECISIONS.md` para el detalle de cada fase y `CLAUDE.md` para el plan
completo.
