# Migraciones SQL congeladas (feature 004-multi-motor-bd)

**Referencia histórica desde 2026-08** — ver el detalle en
[`../schema/README-CONGELADO.md`](../schema/README-CONGELADO.md).

Los scripts `16`–`26b` documentan la evolución del esquema SQL Server de la
Fase 1 (identidad central) y los backfills de gaps. Para instalaciones nuevas
NO se ejecutan: el esquema completo lo aprovisionan las migraciones EF por
proveedor (feature 004). La excepción vigente es
`15_Audit_Mongodb_Bootstrap.json` (MongoDB — fuera del alcance multi-motor),
que sigue siendo el descriptor activo de `DbMigrator audit-bootstrap`.
