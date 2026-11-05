# Implementation Plan: Cargue de archivos planos para la implantación

**Branch**: `016-cargue-archivos-implantacion` (worktree `claude/016-cargue-archivos-9a8f33`) | **Date**: 2026-10-28, rehecho el
2026-10-30 (D10) | **Spec**: [spec.md](spec.md)

**Input**: la spec, aprobada por el dueño el 2026-10-28 con las siete historias, y la decisión **D10** del 2026-10-30
(research.md): la 016 es **una utilidad aparte**, una sesión de Claude que adapta los planos y los entrega en el formato del ERP.
En el ERP sólo se construye lo que falta.

> **Compuerta de inicio**: cumplida el 2026-10-30. `origin/develop` `22a18d89` trae la 015 (PR #18) y la 017 (PR #19, #20); la rama
> se puso al día en `a70c902b`.

## Summary

COOFLOPAL carga sus datos desde los archivos planos tal como salen del sistema anterior, para salir en vivo el 01/12/2026. La carga
tiene dos mitades.

**El kit de adaptación** (`tools/implantacion/`, Python 3 + openpyxl, fuera del producto) lo corre Claude en la sesión de
implantación:

- **Leer el archivo tal como sale**: un **perfil** JSON por archivo y cooperativa, versionado en git. Indica:
  - si es delimitado (separador, calificador, encabezado) o de ancho fijo (posiciones, largo de línea);
  - la codificación;
  - las filas que se saltan;
  - un mapeo por columna destino, con su formato de fecha y número, el recorte, la constante y la equivalencia.
- **Traducir códigos**: tablas de equivalencias en CSV (una a una, varias en una, una en varias, no se migra). Se generan desde el
  formato de la contadora con `equivalencias.py`.
- **Entregar la plantilla exacta del ERP**: un `.xlsx` con los encabezados de la plantilla en la fila 1 (comprobados contra el
  libro que descarga el ERP, `--plantilla`). Lo acompañan un mapa «fila → línea original» y un informe:
  - los errores de lectura por línea, con los códigos de contracts/plantillas.md §1.4;
  - los avisos de codificación;
  - los totales de control y su diferencia contra las cifras de referencia.
- **Traducir la revisión**: `traducir-errores.py` toma el libro de revisión que devuelve el ERP (columnas `resultado` y `errores`) y
  pone cada error en la línea del plano.
- El dueño sube los `.xlsx` en las pantallas de siempre (Revisar → Aplicar). Claude no inicia sesión ni maneja tokens del ERP.

**Lo que falta en el ERP** (mecánica común de la 012, `EjecutorDeImportacion`):

- **Personas y terceros** (US1): plantilla `core.people` con las reglas del alta unitaria. La llave es el **usuario derivado**
  de la 017 (`UsuarioDeDocumento`). Crea por `PersonFactory` y, al actualizar, sólo cambia lo que trae valor, con el cambio campo a
  campo. Nunca escribe las marcas de asociado, empleado ni vendedor.
- **Asociados** (US3): plantilla `core.associates` sobre personas vivas, con la regla del alta de asociado (R9, D8).
- **Invitaciones masivas** (US7): plantilla de dos hojas sobre el formato de usuarios. Emite cada invitación **por el mismo camino de
  la 017** y crea o actualiza el catálogo de cargos (R13).

US2, US4, US5 y US6 se cumplen con el kit, las pantallas de cuadre existentes y el runbook (research D10).

## Technical Context

**Language/Version**: C# / .NET 10.0.5; Blazor (Shared, Web, WASM, MAUI). Kit: Python 3.10+ con openpyxl (ya usado por
`docs/implementacion/generar-plan.py`).

**Primary Dependencies**: ya presentes. Carter, MediatR, FluentValidation, EF Core, ClosedXML, auditoría. **No se agrega ninguna
dependencia** al producto.

**Storage**: **sin cambios de esquema**. Personas, asociados, cargos e invitaciones van a sus tablas de siempre. **No hay migración.**

**Testing**: xUnit (Application con su `TestDb` en memoria, Architecture) para las plantillas; `unittest` de Python para el kit, con
muestras sintéticas; e2e con Testcontainers en PostgreSQL y SQL Server **una sola vez al final**.

**Target Platform**: API Linux en k3s (DEV, QA, PDN); clientes web y MAUI. El kit corre en la máquina de la sesión de implantación.

**Constraints**:
- los topes del ejecutor común (16 MB, 60.000 filas) siguen; el kit parte los planos grandes;
- los defectos son parámetros por cooperativa (el perfil es por cooperativa);
- ningún texto del producto nombra al sistema anterior;
- los planos y libros generados nunca entran al repositorio.

**Scale/Scope**: COOFLOPAL tiene miles de personas y asociados, cientos de miles de cuotas y cientos de usuarios. El ERP gana
3 plantillas, ~6 rutas y botones en 3 pantallas.

## Constitution Check

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | spec aprobada 2026-10-28; D10 rehízo este plan y tasks antes del código |
| II   | Clean Architecture | PASS | plantillas en Application; rutas en la API; el kit es una herramienta fuera de la solución |
| III  | CQRS + MediatR | PASS | cada importación es un comando con su validador, sobre el ejecutor común |
| IV   | Multi-tenancy | PASS | todo en la base de la cooperativa; US7 escribe la base administrativa sólo por el servicio de la 017 |
| V    | Person centralizada | PASS | personas por `PersonFactory` y el validador del alta; marcas de rol sólo por su handler |
| VI   | PublicId | PASS | ninguna ruta nueva expone Id interno |
| VII  | Soft-delete + auditoría | PASS | la plantilla nunca borra; `Import.Reviewed/Applied` con SHA-256 y el interceptor de siempre |
| VIII | Validación dual | PASS | revisión antes de aplicar; FluentValidation en cada comando |
| IX   | Errores visibles | PASS | errores por hoja, fila y columna; el kit los lleva a la línea del plano |
| X    | Trazabilidad | PASS | evento de la importación con el SHA-256; perfiles y equivalencias versionados en git |
| XI   | Inmutabilidad contable | PASS | no toca movimientos |
| XII  | Migraciones | PASS | **ninguna**; ninguna congelada se toca |
| UI   | `IndicadorDeCarga` | PASS | `ImportarPlantilla` ya lo lleva; las zonas nuevas también |

Re-evaluación post-D10: **PASS**, sin desviaciones (las dos de la versión anterior —archivo guardado y compensación entre dos
bases— se van: la primera porque ya no hay cargue en segundo plano, la segunda sigue sólo en US7 y está en R13).

## Project Structure

```text
tools/implantacion/                         # el kit (no es parte del producto ni de la solución)
├── README.md                               # cómo lo usa la sesión de implantación
├── kit/                                    # paquete Python: perfil, lector, conversor, equivalencias, escritor, informe
├── adaptar.py                              # plano + perfil (+ equivalencias, cifras) → libro(s) .xlsx + mapa + informe
├── equivalencias.py                        # formato de la contadora → CSV por tabla
├── traducir-errores.py                     # libro de revisión del ERP + mapa → errores por línea del plano
├── perfiles/ejemplo/                       # perfiles de ejemplo sobre las muestras sintéticas
├── cooperativas/<codigo>/                  # perfiles y equivalencias reales, como dato (sin datos personales)
└── pruebas/                                # unittest + muestras sintéticas

src/Core/IngenIA365ERP.Application/
├── Core/People/Imports/                    # PlantillaDePersonas, ImportPeopleCommand
├── Core/Associates/Imports/                # PlantillaDeAsociados, ImportAssociatesCommand
└── Security/Users/Imports/                 # PlantillaDeInvitaciones, ImportUserInvitationsCommand, RolesDelFormato

src/Presentation/
├── IngenIA365ERP.API/Endpoints/Core/        # rutas de plantilla de personas y asociados
├── IngenIA365ERP.API/Endpoints/Security/    # rutas de la plantilla de invitaciones
└── IngenIA365ERP.Shared/Pages/              # botones en Maestros › Personas, Asociados y Usuarios

docs/operaciones/implantacion-con-el-kit.md  # runbook: orden, cifras, cuadre, ensayo en QA, diferencias aceptadas
```

**Structure Decision**: las plantillas viven en su módulo, como las de la 012, la 013 y la 015. Las de personas y asociados se
registran en `CatalogoDePlantillasDePlataforma` (Core, sin número de orden). El kit es una herramienta del repositorio como
`docs/implementacion/generar-plan.py`, no un proyecto de la solución.

## Dependencias verificadas (2026-10-30)

- **017**:
  - `Domain/Common/Identidad/UsuarioDeDocumento` (normalizar, validar, derivar) y `TiposDeDocumento` (CC→`C`, CE, PPT→`PT`,
    PEP, TI, PA, RC, NIT→`NI`) son funciones puras: T005 no hace falta;
  - `COR_People.Username` con `UK_COR_People_Username`;
  - `PersonFactory.PrepareAsync` busca por usuario, eliminadas incluidas.
- **015**: la plantilla `plataforma.cuentas-bancarias-de-personas` cubre las cuentas de las personas (por eso D1 sale de la plantilla
  de personas).
- La forma del servicio de invitación de la 017 se revisa al empezar US7 (T040).

## Riesgos

- **Formatos raros**: sin muestras reales, el kit cubre los casos de la spec. Un formato nuevo se resuelve en el kit el mismo día, sin
  desplegar el ERP: es la ventaja de D10.
- **Fecha**: go/no-go el 28/11. Orden de construcción: MVP (kit + personas), asociados, invitaciones; luego el runbook y el ensayo.
