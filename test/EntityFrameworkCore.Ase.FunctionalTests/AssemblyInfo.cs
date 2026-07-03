using Xunit;

// Todos los tests de este assembly pegan contra la misma instancia real de ASE (no mocks). Correr
// clases de test en paralelo puede hacer que dos CREATE DATABASE choquen contra la base "model" de
// ASE (la plantilla que clona cada CREATE DATABASE) con "The model database is unavailable" —
// confirmado como flake real al agregar el segundo test que crea una base (ver DECISIONS.md, Fase 6).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
