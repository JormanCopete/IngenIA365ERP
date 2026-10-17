# Runbook: el formato SICSES de cartera y los archivos para centrales de riesgo

> Feature 013, fase 21 (US16, entrega K5). Para quien despliega, para el contador que genera el formato del SICSES y para el responsable de
> los reportes a centrales. Qué hacen las pantallas está en el tema «Formato SICSES y archivos para centrales de riesgo» del manual en línea
> (`/cartera/sicses`, `/cartera/centrales`). Contrato: [api.md](../../specs/013-cartera-financiera/contracts/api.md) §18,
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §28, adaptadores-y-archivos.md §7; decisiones D28, C46, C72, T79.

K5 trae la migración aditiva `CarteraInformes` (sólo `LND_RegulatoryFiles` y `LND_RegulatoryFileLines`), el motor genérico de layouts
(`Domain/Common/Layouts`: `LayoutDeArchivo`, `ValidadorDeLayout`, `EscritorPorLayout`; la PILA no lo usa) y tres layouts embebidos en
`Application/Lending/Regulatory/Layouts/`:

| Archivo | Código | Forma |
|---|---|---|
| `sicses-cartera-1-2026-12-01.json` | `SICSES-CARTERA-v1` | delimitado por barra vertical, una fila por crédito del cierre mensual |
| `central-DATACREDITO-1-2026-12-01.json` | `CENTRAL-DATACREDITO-v1` | ancho fijo de 400, cabecera, detalle y totales |
| `central-TRANSUNION-1-2026-12-01.json` | `CENTRAL-TRANSUNION-v1` | delimitado |

**Los tres están sin cotejar** (`verified = false` en cada campo; fuente «por cotejar», pregunta I7). Mientras tanto **son vigentes** y
cada generación lleva la advertencia `Lending.Regulatory.LayoutSinCotejar`, que se reconoce para generar (precedente D-43 de la PILA). No
subir al portal un archivo de un layout sin cotejar sin que el contador lo haya revisado contra el instructivo.

## 1. Antes de desplegar

1. Respaldo de cada base de cooperativa (Principio XII), aunque `CarteraInformes` sea aditiva.
2. `CarteraInformes` en los dos proyectos de migraciones, detrás de las de K4; `tools/scripts/check-migration-parity.ps1` verde.
3. Los permisos `Lending.RegulatoryFiles.Read`, `.MarkUploaded`, `Lending.RegulatoryReports.Generate` y `Lending.CreditBureauFiles.Generate`
   los siembra la API al arrancar. El perfil sugerido de contador los trae; el revisor fiscal sólo necesita `RegulatoryFiles.Read`.
4. Por cooperativa, el parámetro **`Centrales.CodigosDeSuscriptor`** (`DATACREDITO=…;TRANSUNION=…`): sin él el archivo de la central sale
   sin código de suscriptor y el validador lo señala como bloqueante.
5. El plazo del SICSES por nivel de supervisión sale del dato `sicsesReport` del documento `TiposDeOrganizacion` (perfil regulatorio). Sin
   él la tarea `cartera.vigilancia` no avisa `Cartera.PlazoRegulatorioPorVencer`.

## 2. Generar y cargar el SICSES de un mes

1. El mes tiene que tener su **cierre mensual vigente** (`/cartera/cierres`); sin él, `Lending.MonthlyClose.NotFound`. Un cierre reabierto
   no cuenta: se vuelve a cerrar y se usa la versión nueva.
2. `/cartera/sicses` → año y mes → **Validar**. No guarda nada; lista las inconsistencias por crédito, registro y campo, y la fecha límite.
3. **Generar**. Con bloqueantes queda una versión `Validated` **sin archivo** con lo encontrado (la API responde 422
   `Lending.Regulatory.BlockingIssues`). Se corrige el dato en el maestro (persona, crédito) y, si el dato es del cierre, se reabre y se
   vuelve a cerrar el mes. Sin bloqueantes, la pantalla pide reconocer las advertencias y genera la versión `Generated` con su SHA-256; la
   anterior del período queda `Superseded` con su archivo.
4. **Descargar** (enlace firmado de 60 s, auditado como `Lending.RegulatoryFile.Downloaded`) y cargar en el portal de la Supersolidaria.
5. **Marcar cargado** con el radicado y su fecha. Desde ese momento el período no se regenera (`Lending.Regulatory.AlreadyUploaded`): una
   corrección va en el período siguiente o se tramita con la Superintendencia.

### La marca «modelo obligatorio no disponible» (D28)

Si el cierre del mes se hizo sin el modelo de pérdida esperada obligatorio (`ObligatoryModelUnavailable`), la generación lleva la advertencia
`ModeloObligatorioNoDisponible`, que se reconoce como las demás y queda en la generación (`IssuesJson`, evento de auditoría). **Nunca va dentro
del archivo.** Si el cierre tiene parámetros normativos sin validar, se suma la advertencia de normativa pendiente. El contador decide si
carga el archivo así o espera.

## 3. Generar y cargar el archivo de una central

1. `/cartera/centrales` → central → año y mes → **Validar**. El archivo lleva las novedades desde el **último archivo cargado** de esa central
   (desembolsos, pagos, mora, cancelaciones, castigos) y los retiros de reportes hechos sin aviso.
2. **Generar**, **Descargar**, cargar en el portal de la central y **Marcar cargado** con el radicado, igual que el SICSES. Un período
   anterior al último cargado tampoco se regenera.
3. Al generar (no al validar) se levantan las alertas `Cartera.AvisoPrevioSinConstancia` de los episodios que llegaron a su punto de reporte
   sin constancia.

### Qué hacer con los excluidos

Quien no puede ir al archivo queda en `LND_RegulatoryFileLines` con su motivo y aparece en la pantalla y en el informe
`/cartera/informes?vista=bureau-exclusions`:

| Motivo | Qué hacer |
|---|---|
| Sin autorización de reporte (`NoReportAuthorization`) | obtener la autorización `CreditBureauReport` con su evidencia |
| Sin aviso previo con constancia (`NoPriorNoticeEvidence`) | enviar el aviso previo o registrar su constancia; se reporta desde su fecha más temprana |
| Reclamación al seguro en trámite (`DeceasedClaimInProcess`) | esperar la respuesta de la aseguradora; negada o desistida, vuelve a reportarse |
| Insolvencia (`Insolvency`) | no reportar mientras el proceso negocie |
| Pagaduría en mora dentro del umbral (`PaymasterArrearsWithinThreshold`) | gestionar el giro con la pagaduría (K2) |

El excluido entra solo en el archivo siguiente en cuanto se corrige la causa; no hay que hacer nada en el archivo ya cargado.

## 4. Cotejar un layout (cuando llegue el instructivo)

Un layout es **dato versionado**, no lógica: cotejarlo es publicar una versión nueva del JSON, y **exige despliegue** (va embebido en el
ensamblado de Application).

1. Copiar el JSON vigente con el nombre de la versión nueva: `sicses-cartera-{version}-{aaaa-mm-dd}.json` o
   `central-{CODIGO}-{version}-{aaaa-mm-dd}.json` (la fecha es `validFrom`; el catálogo comprueba que nombre, clase y central correspondan).
2. En el nuevo: `code` con la versión (`SICSES-CARTERA-v2`), `version`, `validFrom`, `source` con el instructivo exacto, y en cada campo
   cotejado `verified: true`. Arriba, `verifiedBy`, `verifiedOn` y `verifiedAgainst` (el instructivo y el archivo aceptado por el validador).
3. En el anterior, cerrar `validTo` la víspera del `validFrom` nuevo: dos vigentes cruzados de la misma clase y central **impiden arrancar**
   (`CatalogoDeLayoutsDeCartera` valida todo al componer Application; un JSON mal formado tampoco arranca).
4. Si cambia un `path` o un código traducido, se cambia también la proyección (`ProyeccionSicsesCartera`, `ProyeccionCentralDeRiesgo`) o
   `TraduccionesRegulatorias`: un `path` es el vocabulario que entrega la proyección.
5. Regenerar los casos dorados **a propósito** (siguiente sección) y revisar el diff byte a byte antes del commit.
6. `GET /api/lending/regulatory/layouts` muestra los campos cotejados; la advertencia `LayoutSinCotejar` desaparece cuando ya no queda
   ninguno sin cotejar.

## 5. Regenerar los casos dorados

Los archivos esperados están en `tests/IngenIA365ERP.Application.Tests/Lending/Regulatory/Casos/*.esperado.txt` y la prueba
`CasosDoradosDeLayoutsDeCarteraTests` los compara byte a byte. Sin la variable nunca escribe. Para reescribirlos (sólo tras cotejar a mano):

```powershell
$env:CARTERA_ESCRIBIR_ESPERADO = "1"
dotnet test tests/IngenIA365ERP.Application.Tests --filter CasosDoradosDeLayoutsDeCarteraTests
Remove-Item Env:CARTERA_ESCRIBIR_ESPERADO
dotnet test tests/IngenIA365ERP.Application.Tests --filter CasosDoradosDeLayoutsDeCarteraTests   # tiene que pasar sin la variable
git diff tests/IngenIA365ERP.Application.Tests/Lending/Regulatory/Casos/
```

## 6. Verificación

- `GET /api/lending/regulatory/layouts` responde los tres layouts con `unverifiedFields`.
- Con un mes cerrado: validar, generar con las advertencias reconocidas, descargar y comparar el SHA-256 con el de la pantalla.
- La e2e `SicsesYCentralesTests` (T0993) recorre el ciclo por HTTP en los dos motores; se corre en la corrida final de la feature.
- Pendiente del dueño (T1011): pasar el archivo del SICSES de un cierre de prueba por el validador de la Superintendencia y el de cada
  central por su validador, y cotejar campo por campo contra el instructivo (SC-019).
