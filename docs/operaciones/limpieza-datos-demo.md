# Limpieza de datos de demostración (feature 004)

Todo registro sembrado por el seed de pruebas lleva la marca reconocible
`CreatedBy = 'system:seed-demo'` (FR-020). Si los datos demo llegaron a un
ambiente equivocado (activación accidental de `Database:Seed:RunTestSeed` —
que además queda auditada en Mongo con el evento
`Database.Seed.TestSeedEnabledInProduction`), este es el procedimiento.

## 1. Localizar

```sql
-- SQL Server
SELECT 'COR_People' AS Tabla, COUNT(*) FROM dbo.COR_People WHERE CreatedBy = 'system:seed-demo';
-- PostgreSQL
SELECT 'COR_People' AS tabla, COUNT(*) FROM dbo."COR_People" WHERE "CreatedBy" = 'system:seed-demo';
```

Repetir por cada tabla que el `DemoDataSeeder` cubra (hoy: `COR_People`; el
inventario crece con el seeder — consultarlo en
`src/Infrastructure/IngenIA365ERP.Persistence/Seeding/Demo/DemoDataSeeder.cs`).

## 2. Depurar (mantenimiento técnico autorizado)

Conforme al principio constitucional XI, la eliminación se ejecuta SOLO como
mantenimiento técnico autorizado (administrador con justificación escrita y
backup previo), NUNCA como flujo de aplicación:

1. Backup completo de la base afectada.
2. Soft-delete preferente:
   `UPDATE ... SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), DeletedBy = 'ops:demo-cleanup' WHERE CreatedBy = 'system:seed-demo'`.
3. Si el seeder llegara a incluir movimientos contables demo (hoy NO los
   incluye a propósito), estos jamás se editan/borran: se asientan reversos.
4. Registrar la intervención (ticket + evidencia) y desactivar
   `Database:Seed:RunTestSeed` en la configuración del ambiente.
