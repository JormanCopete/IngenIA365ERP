# Quickstart: Alta de persona en un paso desde los módulos

**Feature**: 008 | **Date**: 2026-09-13

Cómo comprobar la feature de punta a punta, en el orden en que conviene hacerlo.

## 1. Compilar y pruebas sin contenedores

```bash
dotnet build IngenIA365ERP.CI.slnf -c Release
```

```bash
dotnet test tests/IngenIA365ERP.Application.Tests --filter "FullyQualifiedName~People|FullyQualifiedName~EmployeeManagement|FullyQualifiedName~Associates|FullyQualifiedName~Permissions"
```

```bash
dotnet test tests/IngenIA365ERP.Architecture.Tests
```

Deben aparecer verdes, entre otras: `PersonFactoryTests` (duplicada viva → `TaxIdDuplicate`;
eliminada → `TaxIdDeleted`; nunca segunda fila), `RestorePersonCommandHandlerTests`,
`UpdatePersonCommandHandlerTests` (asociado+empleado siguen `true`),
`RegisterEmployeeWithPersonCommandHandlerTests` (un solo `SaveChanges`; plan inexistente no
persiste nada), `RepartoDePermisosTests`, `LaPersonaSeEscribeEnUnSoloSitio`,
`LosMaestrosDePersonaExigenPermiso`, `LasPantallasDicenQueEstanCargando` (con `Asociados`).

## 2. Pruebas de integración (Docker Desktop encendido)

```bash
dotnet test tests/IngenIA365ERP.API.IntegrationTests --filter "FullyQualifiedName~AltaDePersonaEnUnPaso"
```

`Core/AltaDePersonaEnUnPasoTests` recorre por HTTP: `with-person` 201 + `isEmployee` +
`by-person`; documento duplicado 422 sin registrar nada; documento de eliminada 422
`Person.TaxIdDeleted` sin fila nueva; `/restore` con Operador → 404 y con admin → 204 con
banderas recalculadas; sólo lectura → 404 idéntico a ruta inexistente; asociado → empleado →
`PUT` persona conserva ambas banderas; `terminate` apaga sólo `IsEmployee`; `terminate` +
reingreso → dos fichas y `by-person` devuelve la viva; `/permissions/mine` por rol;
`/search?rol=`.

## 3. Local, a mano (API + Web en Development)

Antes de arrancar, la migración de datos y los seeders corren solos (`AutoMigrate` +
`PhaseZeroSecuritySeeder`): en el log de la API deben verse `CorePermissionCatalogSeeder` y
`ConcederLecturaDeMaestrosATodosLosRoles` sin errores.

1. **Operador** (rol built-in `Operator`): `/nomina/empleados` → «Nuevo empleado» → documento
   inexistente → «Crear persona nueva con "…"» → pestañas Identificación / Contacto / Demografía
   editables + Datos laborales + Banca → «Registrar» → toast «Persona y empleado registrados».
   Verificar en `/maestros/personas` que aparece con el distintivo «Empleado» **no editable** y,
   en Roles, sin botón «Registrar como empleado…». Repetir en `/asociados/registro`.
2. **Mismo documento otra vez** → 422 con «Ya existe {nombre}…» y botón «Usar esa persona» →
   se reabre con la persona existente, datos personales **deshabilitados**, botón «Editar datos
   de la persona» (Operador tiene `Core.People.Update`) que abre el diálogo compartido; al
   guardar, el diálogo del módulo refleja el cambio.
3. **Banderas**: registrar como empleada a una persona asociada → sigue asociada; editar su correo
   en Personas → conserva ambos distintivos; «Terminar contrato» (con CompanyAdmin: Operador no
   lo ve) → pierde sólo «Empleado». Volver a «Nuevo empleado» con esa persona → modo registro
   (ficha nueva); en la grilla quedan dos filas: «Retirado» y «Activo».
4. **Eliminada**: como CompanyAdmin eliminar una persona sin cartera ni contrato activo; luego,
   como Operador, intentar crearla → 422 «pertenece a una persona eliminada el …» **sin** botón
   de restaurar y con el texto de pedirla; como CompanyAdmin → botón «Restaurar persona» → sigue
   el alta como persona existente.
5. **Sólo lectura** (`ReadOnly`): no ve «Nuevo empleado», «Nuevo asociado» ni «Nueva persona»;
   el picker no ofrece crear; con `curl` un `POST /api/payroll/employees` con su token responde
   404 `Generic.NotFound`, igual que `POST /api/payroll/no-existe`.
6. **Cambio de cooperativa** con dos cooperativas y permisos distintos: los botones se recalculan
   sin F5 (mirar en la red una sola llamada a `/api/admin/permissions/mine` por cooperativa).
7. **Slow 3G** en el navegador: velos de `IndicadorDeCarga` en el picker («Buscando…»), en el
   diálogo («Cargando…» / «Guardando…») y en `RegistroAsociado`.

## 4. QA y producción

- QA: repetir §3 con los tres roles y una cooperativa con roles **personalizados** creados
  antes del despliegue: deben conservar la consulta de Personas, Empleados y Asociados (FR-010).
- Antes de promover a producción: `pg_dump` de cada base de cooperativa y correr
  [diagnostico-banderas.sql](diagnostico-banderas.sql) (sólo lectura) antes y después; después
  las seis filas de desalineación deben dar 0.

```bash
ssh -i ~/.ssh/ingenia365_deploy root@100.104.190.76 "k3s kubectl -n erp-pdn exec -i erp-db-1 -c postgres -- psql -U postgres -d cooflopal -At" < specs/008-alta-persona-un-paso/diagnostico-banderas.sql
```

- Release notes para las cooperativas: los códigos de permiso nuevos y que **la escritura** de
  Personas/Empleados/Asociados la debe asignar el administrador a los roles personalizados.
