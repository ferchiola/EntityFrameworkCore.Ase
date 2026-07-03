# EntityFrameworkCore.Ase

Proveedor de Entity Framework Core para SAP ASE (Adaptive Server
Enterprise, ex-Sybase), escrito desde cero siguiendo la arquitectura
relacional actual de EF Core (9.x).

> Nombre provisorio. `EntityFrameworkCore.Ase` ya existe como paquete en
> NuGet.org (de `spasarto`, sin actividad desde 2022, sin licencia
> declarada) — antes de publicar este paquete hay que definir un nombre
> propio (ver `DECISIONS.md`).

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

En Fase 0 (investigación + setup). Ver `DECISIONS.md` para el detalle de
cada fase y `CLAUDE.md` para el plan completo.
