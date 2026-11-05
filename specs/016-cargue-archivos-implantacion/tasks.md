# Tasks: Cargue de archivos planos para la implantación

**Input**: los documentos de `specs/016-cargue-archivos-implantacion/`. Esta lista se **rehízo el 2026-10-30** con la decisión D10
(research.md): la 016 es una utilidad aparte. Una sesión de Claude adapta los planos con el kit de `tools/implantacion/`, y el ERP sólo
gana las plantillas que faltan (US1, US3, US7). La lista anterior (T001–T109, con perfiles, equivalencias, tablero y cargue en
segundo plano dentro del ERP) queda en el historial de git (`dc1ac6fd`).

**Tests**:
- Las pruebas unitarias del kit (`unittest`), de Application y de arquitectura se escriben en cada fase.
- La compilación se comprueba en cada fase.
- **Las suites completas y las e2e se corren una sola vez al final** (T060), en PostgreSQL y SQL Server.
- Todo con **muestras sintéticas**.

**Orden**: Kit (US2 + US4 + US5 del lado de la adaptación) → US1 (MVP) → US3 → US7 → runbook (US5 cuadre, US6) → Polish.

Rutas: `src/Core/IngenIA365ERP.Application` (App), `src/Presentation/IngenIA365ERP.API` (API), `src/Presentation/IngenIA365ERP.Shared`
(Shared), `tools/implantacion` (Kit).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup (compuerta de dependencias)

- [X] T001 Verificar que `origin/develop` trae la 015 (PR #18) y la 017 (PR #19, #20) y poner la rama al día: `22a18d89`, mezclado en `a70c902b`.
- [X] T002 Comprobar las piezas de la 017 que usa la 016 y anotarlas en plan.md («Dependencias verificadas»): `UsuarioDeDocumento` y `TiposDeDocumento` son puras en Domain, `COR_People.Username` y `PersonFactory` por usuario. La extracción de la vieja T005 no hace falta.
- [X] T003 Registrar la decisión D10 en research.md y rehacer plan.md y esta lista.

---

## Phase 2: Kit de adaptación (US2, y la lectura de US4 y los totales de US5) 🎯 MVP

**Goal**: convertir un plano tal como sale (delimitado o de ancho fijo) en la plantilla exacta del ERP, con equivalencias, errores por
línea y totales de control.

**Independent Test** (la de la US2, en el kit):
- un perfil CSV con «;» y fechas `dd/MM/yyyy`, y otro de ancho fijo en windows-1252;
- `adaptar.py` produce el libro con los encabezados de la plantilla y los valores en texto canónico;
- un campo fuera de la línea, una fecha imposible y un código sin equivalencia salen con su línea y su código de error;
- las equivalencias se generan desde el formato de la contadora; un código repetido con destinos distintos se rechaza con las dos filas.

- [X] T004 [P] [US2] Muestras sintéticas sin datos reales en tools/implantacion/pruebas/muestras/: `terceros-ancho-fijo.txt` (windows-1252), `terceros.csv`, `asociados.csv`, `equivalencias-contadora.xlsx` (generado por la prueba) y `revision-erp.xlsx` (generado por la prueba).
- [X] T005 [US2] `kit/perfil.py`: carga y valida el perfil JSON (`version`, `destino`, `archivo` delimitado o de ancho fijo, `codificacion`, `saltar`, `columnas[]` con `origen` | `posicion`+`largo` | `constante`, `tipo` texto/fecha/numero/sino, `formato`, `decimalesImplicitos`, `signo`, `recorte`, `siVacio`, `equivalencia`). Un destino repetido, posiciones cruzadas o una obligatoria sin fuente (contra `--plantilla`) se rechazan nombrando la columna.
- [X] T006 [US2] `kit/lectura.py`: decodifica (utf-8 con o sin BOM, windows-1252, iso-8859-1; cuenta `U+FFFD` y controles → `Import.Profile.EncodingSuspect`), salta filas (primeras N, vacías, patrones `empiezaCon`/`contiene`/`regex`) y parte delimitado (calificador) o ancho fijo. Detecta `Import.Profile.LineLength`, `FieldOutOfLine` y `MissingColumn` y conserva el número de línea original.
- [X] T007 [US2] `kit/conversion.py`: fechas exactas por formato (vacíos y ceros = «sin fecha»; imposible → `Import.Profile.InvalidDate`) y números con miles, decimal, decimales implícitos y signo adelante/atrás/paréntesis/`CR-DB` (nunca cero ante uno que no encaja → `InvalidNumber`). Sí/no → `si`/`no`. Texto recortado.
- [X] T008 [US2] `kit/equivalencias.py`: carga tablas CSV (`codigoAnterior,codigoNuevo,clase,nota`). Clases `una-a-una`, `varias-en-una`, `una-en-varias` (→ `Import.Equivalence.OneToMany` con los destinos) y `no-se-migra` (→ `NotMigrated`, D5); un faltante → `Missing` con tabla y código. Rechaza el mismo código anterior con destinos distintos (salvo `una-en-varias`) con las dos filas.
- [X] T009 [US2] `kit/escritor.py`: escribe el libro (hoja `Datos`, encabezados exactos en la fila 1, todo como texto), lo parte en varios con `--max-filas` (60.000 por defecto) y deja el mapa CSV «libro, fila → línea original». **Se niega a escribir dentro del repositorio.**
- [X] T010 [US2] `kit/totales.py`: totales de control por archivo (filas leídas, saltadas, con error, entregadas; cuenta por columna de agrupación; suma por columna numérica) y comparación contra un CSV de cifras de referencia (`total,valor`), con la diferencia.
- [X] T011 [US2] `adaptar.py` (CLI): plano + perfil + `--equivalencias carpeta` + `--plantilla libro.xlsx` del ERP (opcional, comprueba encabezados y obligatorias) + `--cifras` + `--trabajo carpeta` → libro(s), mapa e informe (`informe.md` y `errores.csv`). Código de salida ≠ 0 si hay errores de lectura.
- [X] T012 [US2] `equivalencias.py` (CLI): la hoja «Equivalencias» del formato de la contadora (columnas «Código actual», «Cuenta CUIF (6 dígitos)», «Auxiliar nuevo (código)», «Tipo de equivalencia»; research R7) → `CUENTAS.csv`, con los conflictos por fila.
- [X] T013 [US2] `traducir-errores.py` (CLI): libro de revisión del ERP (columnas `resultado` y `errores`) + mapa → `errores-por-linea.csv` con la línea del plano, la columna y el mensaje.
- [X] T014 [P] [US2] Perfiles de ejemplo sobre las muestras en tools/implantacion/perfiles/ejemplo/ (`terceros-ancho-fijo.json`, `terceros-csv.json`, `asociados-csv.json`) y equivalencias de ejemplo (`CIUDADES.csv`, `TIPOS_DOCUMENTO.csv`, `OFICINAS.csv`).
- [X] T015 [US2] Pruebas `unittest` en tools/implantacion/pruebas/ (escenarios 1–5 de la US2 en el kit, conversión, equivalencias, partición, mapa, traducción de errores, negativa a escribir en el repositorio).
- [X] T016 [US2] tools/implantacion/README.md: cómo lo usa la sesión de implantación (carpeta de trabajo fuera del repositorio, orden de los pasos, qué hace el dueño en pantalla) y `.gitignore` de la carpeta.

**Checkpoint**: el kit convierte las muestras y pasa sus pruebas.

---

## Phase 3: User Story 1 - Personas y terceros (Priority: P1) 🎯 MVP

**Goal**: cargar personas naturales y jurídicas con las reglas del alta unitaria.

**Independent Test**:
- revisar un archivo con 3 filas buenas y 2 malas da errores con fila y columna y no guarda nada;
- corregido y aplicado, deja las 5 personas como las deja el alta unitaria, con su evento.

- [X] T017 [US1] `PlantillaDePersonas` (contracts/plantillas.md §2, sin las columnas bancarias: D10/D1) en src/Core/IngenIA365ERP.Application/Core/People/Imports/PlantillaDePersonas.cs, registrada en `CatalogoDePlantillasDePlataforma` (ruta `/api/core/people`, `Core.People.View` / `Core.People.Create`).
- [X] T018 [US1] `ImportPeopleCommand` + validador + handler en el ejecutor común, en src/Core/IngenIA365ERP.Application/Core/People/Imports/ImportPeopleCommand.cs. Reglas:
  - tipo y número por `UsuarioDeDocumento.Validar`;
  - diccionario `Username → persona` en bloque, eliminadas incluidas → `Person.TaxIdDeleted` en la columna del documento;
  - `Import.Row.Duplicate` dentro del archivo;
  - el mismo `PersonInputValidator`;
  - crear por `PersonFactory.PrepareAsync`;
  - actualizar sólo lo que trae valor, con el cambio campo a campo;
  - `esProveedor`/`esCliente` vacíos no cambian, y nunca toca asociado, empleado ni vendedor.
- [X] T019 [US1] Rutas `GET /api/core/people/template.xlsx` y `POST /api/core/people/import?mode=` por `MapPlantillaDePlataforma` en src/Presentation/IngenIA365ERP.API/Endpoints/Core/PeopleEndpoints.cs (o su módulo de rutas).
- [X] T020 [US1] Sección «Importar personas» con `ImportarPlantilla` en Maestros › Personas (src/Presentation/IngenIA365ERP.Shared/Pages/Maestros/Personas.razor), con su `PermissionGate` y `IndicadorDeCarga`.
- [X] T021 [P] [US1] Pruebas de `ImportPeopleCommand` en tests/IngenIA365ERP.Application.Tests/Implantacion/Personas/ImportarPersonasTests.cs:
  - crear y actualizar con cambio campo a campo;
  - eliminada, llave repetida, CC y CE con el mismo número como dos personas;
  - marcas de proveedor y cliente, y nunca las de rol;
  - revisión que no guarda.
- [X] T022 [P] [US1] Prueba de arquitectura: la plantilla de personas no escribe `IsAssociate`, `IsEmployee` ni `IsSalesperson` y crea por `PersonFactory`, en tests/IngenIA365ERP.Architecture.Tests/Principles/LaPlantillaDePersonasUsaElAltaUnitaria.cs.
- [X] T023 [P] [US1] e2e de la US1 (escenarios 1–6) en tests/IngenIA365ERP.API.IntegrationTests/Implantacion/CargueDePersonasTests.cs (escrita; se corre en T060).
- [X] T024 [US1] `dotnet build IngenIA365ERP.slnx` sin errores (2026-10-30: 0 errores; las 11 pruebas de Application y las 2 de arquitectura nuevas, corridas con filtro, pasan).

**Checkpoint**: MVP (kit + personas) completo.

---

## Phase 4: User Story 3 - Asociados (Priority: P2)

**Independent Test**: 10 personas creadas; un documento inexistente impide aplicar. Corregido, quedan 10 asociados con su marca.

- [X] T025 [US3] Extraer de `RegisterAssociateCommand`/`UpdateAssociateCommand` el servicio `AltaDeAsociado`, que aplica la regla sin guardar; los dos handlers lo usan y sus pruebas siguen verdes. Va en src/Core/IngenIA365ERP.Application/Core/Associates/Services/AltaDeAsociado.cs. (Hecho sobre `AssociateRegistrar`, que ya era ese servicio desde la 008: `Construir`, sin consultas, y `RetiroQueEsDeCartera`; un segundo servicio habría partido la regla.)
- [X] T026 [US3] `PlantillaDeAsociados` e `ImportAssociatesCommand` (contracts/plantillas.md §3, research R9, D8) en src/Core/IngenIA365ERP.Application/Core/Associates/Imports/ImportAssociatesCommand.cs.
- [X] T027 [US3] Rutas de plantilla en `/api/core/associates` (`Core.Associates.View` / `.Create`).
- [X] T028 [US3] Sección de importación en la pantalla de asociados, con `IndicadorDeCarga`.
- [X] T029 [P] [US3] Pruebas de la plantilla en tests/IngenIA365ERP.Application.Tests/Implantacion/Asociados/ImportarAsociadosTests.cs:
  - «esta plantilla no crea personas»;
  - actualizar sin duplicar;
  - retirado al crear;
  - vigente → retirado rechazado;
  - afiliación futura.
- [X] T030 [P] [US3] e2e de la US3 en tests/IngenIA365ERP.API.IntegrationTests/Implantacion/CargueDeAsociadosTests.cs (escrita; se corre en T060).
- [X] T031 [P] [US3] Perfil de ejemplo de asociados en el kit con su equivalencia de oficinas (escenario independiente por el kit). (`tools/implantacion/perfiles/ejemplo/asociados-csv.json` con `equivalencias/OFICINAS.csv`, ya en el kit.)

---

## Phase 5: User Story 7 - Invitaciones masivas (Priority: P2)

**Independent Test**: un libro de 5 filas, con una confirmación distinta y un cargo inexistente, no envía nada. Corregido, salen 5
invitaciones con el mismo mensaje, el cargo nuevo y un evento de la importación.

- [ ] T040 [US7] Revisar el servicio de invitación de la 017. Si hace falta, partirlo en validar, emitir sin guardar y enviar después de confirmar, sin cambiar el comportamiento unitario.
- [ ] T041 [US7] `TablaLeida.Normalizar` ignora un `*` final (src/Core/IngenIA365ERP.Application/Common/Interfaces/Files/ITabularFileReader.cs), con su prueba.
- [ ] T042 [P] [US7] `RolesDelFormato` en src/Core/IngenIA365ERP.Application/Security/Users/Imports/RolesDelFormato.cs.
- [ ] T043 [US7] `PlantillaDeInvitaciones` e `ImportUserInvitationsCommand` (research R13) en src/Core/IngenIA365ERP.Application/Security/Users/Imports/.
- [ ] T044 [US7] Libro de la plantilla (hojas «Usuarios nuevos», «Cargos», «Listas») y rutas `GET /api/admin/users/invitations/template.xlsx`, `POST …/import?mode=` (`Security.Users.ImportInvitations`).
- [ ] T045 [US7] Pantalla «Invitar desde archivo» con `IndicadorDeCarga` y el botón en la lista de usuarios.
- [ ] T046 [P] [US7] Pruebas de la plantilla (escenarios 1–9 de la US7) y prueba de arquitectura `LasInvitacionesMasivasUsanElCaminoUnitario`.
- [ ] T047 [P] [US7] e2e de la US7 (incluye el escenario 10 y SC-009).

---

## Phase 6: Cargas existentes, cuadre y ensayo (US4, US5, US6, por el kit y el runbook)

- [ ] T050 [P] [US4] Perfiles de ejemplo para la apertura, auxiliares, partes de la carga de migración de Cartera y de aportes, con la prueba de que el libro del kit tiene los encabezados de cada plantilla. Los encabezados salen de las definiciones (`DefinicionesDeMigracion`, `DefinicionesDeMigracionDeCaptaciones`, apertura, auxiliares), exportados por una prueba de Application a un JSON que el kit lee.
- [X] T049 [US4] Plan de cuentas de COOFLOPAL desde su balance con la columna de cuenta deseada: tools/implantacion/cooperativas/cooflopal/plan-de-cuentas.py (libro de carga, libro de validación para la contadora y equivalencias/CUENTAS.csv).
- [ ] T051 [US5] Runbook docs/operaciones/implantacion-con-el-kit.md:
  - orden de los cargues (personas → cuentas bancarias → asociados → auxiliares → apertura → cartera → aportes → usuarios);
  - cifras de referencia por paso;
  - cuadre contra la contabilidad en las pantallas existentes;
  - diferencias aceptadas con motivo en el informe de implantación.
- [ ] T052 [US6] Sección del ensayo en QA con copia de producción en el mismo runbook (FR-026):
  - respaldo de origen, restauración y usuarios sólo del equipo;
  - borrado y constancia;
  - nada en producción sin autorización expresa del dueño.

---

## Phase 7: Polish

- [ ] T055 [P] Prueba de arquitectura `NingunTextoNombraAlSistemaAnterior` para las plantillas y pantallas nuevas.
- [ ] T056 [P] Manuales docs/manual/implantacion-cargue-de-archivos.md y docs/manual/invitaciones-masivas.md.
- [ ] T057 Actualizar CLAUDE.md (sección de la 016) y docs/INDICE-DOCUMENTACION.md.
- [ ] T060 Correr una sola vez: `dotnet build`, las suites sin contenedores, las pruebas del kit y las e2e de la feature en PostgreSQL y SQL Server; anotar los totales.
- [ ] T061 Recorrer quickstart con las muestras en local (web); dejar anotado lo de MAUI y QA.

---

## Dependencies & Execution Order

- Fase 1 hecha. Fase 2 (kit) y Fase 3 (personas) son independientes entre sí; juntas son el MVP.
- US3 depende de US1. US7 depende sólo de la 017. La fase 6 se beneficia de todas.
- T060 es la única corrida de suites completas y e2e.
