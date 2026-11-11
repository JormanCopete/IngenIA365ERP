# Feature 018: Sede principal automática y oficinas dentro de cada empresa

**Estado**: aprobada por el dueño el 2026-11-06. **Depende de**: PR #38 (oficinas acotadas por cooperativa).

## Problema

- Registrar una empresa no crea oficina ni agencia. El método que crea la oficina matriz sólo lo llama una ruta de
  reparación que ninguna pantalla usa.
- `POST /api/tenants` guarda la fila de la empresa sin base de datos y ninguna pantalla la usa.
- Crear una oficina pide escribir el identificador interno de la empresa: sólo es practicable para quien administra la
  consola. Editarla no carga los datos ni deja guardar, porque el código obligatorio queda vacío.
- El maestro ve las oficinas de todas las empresas aunque tenga una activa.
- El menú dice «Sucursales» y la pantalla de Agencias remite a «Administración › Oficinas».

## Decisiones del dueño

| # | Decisión |
|---|---|
| D1 | La sede se llama «Oficina principal» y su código es `PRINCIPAL`. Lo ya creado con otro nombre no se renombra. |
| D2 | Con empresa activa, todos, también el maestro, trabajan sólo sobre ella. Sin empresa activa, el maestro consulta pero no escribe. Ajuste respecto al plan: el maestro no puede entrar a una empresa de la que no es miembro, y la reparación por `POST /api/tenants/{id}/provision` le queda como rescate. |
| D3 | «Nueva oficina» crea también su agencia vinculada, en un paso. Desmarcable. |

## Historias

**US1: empresa nueva con sede.** Al registrar una empresa quedan creadas la oficina principal y su agencia, vinculadas.
La agencia nace sin municipio. Si falla, la empresa queda «Fallida» y la reparación lo completa.

**US2: empresas existentes.** Al arrancar y al reparar se aplica esta tabla. Se cuentan también los registros dados de
baja: si alguien retiró su agencia, no reaparece sola.

| Oficinas | Agencias | Acción |
|---|---|---|
| Ninguna | Ninguna | Crea las dos |
| Alguna, con principal | Ninguna | Crea la agencia vinculada a la principal |
| Alguna, sin principal | Ninguna | No toca nada; avisa en el log |
| Ninguna | Alguna | No toca nada; avisa en el log |
| Alguna | Alguna | No toca nada, aunque no estén vinculadas |

**US3: oficinas dentro de cada empresa.** Crear ya no pide identificadores: la empresa es la activa. Editar muestra los
datos y guarda. El menú dice «Oficinas». La oficina principal no deja de serlo desde la pantalla.

## Requisitos

- FR-001 Un solo responsable crea la sede; repetirlo no duplica nada (idempotente, también tras un fallo a mitad de camino).
- FR-002 La creación automática queda en la auditoría (`Tenant.HeadquartersCreated`).
- FR-003 `POST /api/admin/branches` deja de recibir la empresa; un cuerpo que la traiga se ignora.
- FR-004 Si la agencia de «Nueva oficina» no se puede crear, el código repetido se avisa antes de crear la oficina. Un
  fallo posterior se responde `Branch.AgencyNotCreated` y dice que la oficina quedó creada.
- FR-005 `POST /api/tenants` se retira sin alias.
- FR-006 Sin migraciones: todas las tablas y columnas existen.

## Fuera de alcance

Unir oficina y agencia en una sola entidad. Pedir el municipio al crear. Renombrar la sede de las empresas existentes.
