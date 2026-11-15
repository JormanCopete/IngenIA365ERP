# Plan: feature 018

## Constitución

- **I (Spec-First)**: toca varias capas; esta carpeta es su especificación, plan y tareas.
- **II (Clean Architecture)**: la lógica vive en Application; la base de otra empresa se abre con `ITenantDbContextFactory`.
- **IV (Multi-tenancy)**: la oficina va en la base administrativa y la agencia en la base de la empresa, siempre por su
  `DatabaseName`, nunca por el contexto de la petición, porque el maestro no tiene empresa activa al registrar.
- **VI**: la pantalla y la API sólo manejan `PublicId`.
- **VII**: las entidades ya son auditables; la creación automática deja evento.
- **IX**: los fallos se ven: estado «Fallida», `Branch.AgencyNotCreated` y avisos en el log.
- **XII**: no hay migración.

## Diseño

`ISedePrincipalDeCooperativa.AsegurarAsync(tenantId, ct)` en `Application/Admin/Tenants/SedePrincipal`. Son dos bases sin
transacción común, así que la idempotencia reemplaza a la atomicidad: la oficina se guarda primero y, si la agencia
falla, la siguiente pasada cae en «alguna oficina con principal, ninguna agencia» y la completa. Dos réplicas a la vez
chocan con los índices únicos existentes, nunca duplican.

Puntos que la llaman: registro de empresa, reparación por `/provision` y el arranque
(`PonerAlDiaLasCooperativasAsync`).

`AlcanceDeSucursales` resuelve primero la empresa activa para todos y deja al maestro sin acotar sólo si no tiene
ninguna. `ResolverParaEscribirAsync` exige empresa activa.

`CreateBranchCommand` pierde `TenantPublicId` y gana `CrearAgencia` (por defecto `false` en la API; la pantalla manda
`true`). La agencia va por el `CreateBranchCommand` de Core, que ya valida código y vínculo.

## Riesgos

- E2e que dependan de que una empresa nazca sin agencias: se corre el conjunto de identidad, contabilidad e inventario.
- `PRINCIPAL` podría chocar con un código elegido por la cooperativa: sólo se crea si no tiene ninguna oficina.

## Despliegue

Antes de subir a producción: contar por empresa, sólo con lectura, oficinas y agencias, y mostrar al dueño qué se
crearía. Requiere su autorización.
