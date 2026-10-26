# Cartera: índice de manuales y runbooks

> Feature 013 (Cartera financiera), fase 24 (T1072), y feature 014 (captaciones y aportes), T226. Cartera es el módulo financiero del sector
> solidario que reemplaza al heredado: la 013 construyó los **créditos** (K1 a K6b) y la 014 suma, dentro del mismo módulo, las **captaciones**
> (ahorro a la vista, permanente y contractual, CDAT) y los **aportes sociales** (K7 a K11). Se construyó por **entregas**: K1 es el mínimo para
> salir en vivo y **no sale en producción sin Recaudo y Tesorería reales** (Q1 B); las demás se suman sobre K1, y las de captaciones salen
> **todas juntas**, nunca una sola (la entrega vigente es K11). Este índice enlaza, por entrega, los manuales de uso (`docs/manual`) y los
> procedimientos de operación (`docs/operaciones`). Las especificaciones están en `specs/013-cartera-financiera/` y
> `specs/014-ahorros-aportes-cdt/`.

## Cómo está armado Cartera, en cinco líneas

1. **Un solo escritor del libro** (`RegistroDeCartera`) y **una sola fuente de saldos**: la reconstrucción del crédito a una fecha y con la
   información conocida hasta un momento (D, R). Lo que se ve en una lista es una foto; la cifra exacta es la liquidación.
2. **Cartera no conoce cuentas ni escribe asientos**: emite hechos a Contabilidad por la bandeja de mensajes, en la misma transacción que
   su operación; Contabilidad los asienta por su matriz.
3. **Cartera no aplica pagos por pantalla**: los aplican Recaudo y Nómina por el **contrato de obligaciones** (versión 1).
4. **Los valores de la ley** (usura, UVR, festivos, límites) los registra el maestro en un **registro central** y cada cooperativa lee su
   **réplica**; nada está escrito en el código.
5. **Lo cerrado no se edita**: cierres, actas, documentos, archivos regulatorios y movimientos se corrigen con otro registro, nunca
   cambiando el anterior.
6. **Una Cartera, tres naturalezas** (feature 014): las líneas son de colocación, captación o aportes y comparten catálogo, alcance, actas,
   matriz contable y cierre; las cuentas de captación y aportes tienen su propio escritor del libro (`RegistroDeCuentas`) y se tocan sólo por el
   **contrato de cuentas**, como los créditos por el de obligaciones. El índice propio de captaciones es
   [cartera-captaciones.md](cartera-captaciones.md).

## K1 — el núcleo (salida en vivo)

| Tema | Manual | Operación |
|---|---|---|
| Gobierno, alcance, atribuciones, actas, vinculados, habeas data y mensajes al deudor | [cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md) | [canales-externos.md](../operaciones/canales-externos.md) |
| Líneas de crédito y usura | [cartera-lineas-de-credito.md](cartera-lineas-de-credito.md) | [valores-nacionales.md](../operaciones/valores-nacionales.md) |
| Simulación, solicitudes, decisiones, cupos y bloqueos | [cartera-originacion.md](cartera-originacion.md) | — |
| Desembolso, garantías y pagaré | [cartera-desembolso-y-garantias.md](cartera-desembolso-y-garantias.md) | [cartera-dobles-de-prueba.md](../operaciones/cartera-dobles-de-prueba.md) |
| Causación diaria y liquidación | [cartera-causacion-y-liquidacion.md](cartera-causacion-y-liquidacion.md) | [cartera-proceso-diario.md](../operaciones/cartera-proceso-diario.md) |
| Pagos y contrato de obligaciones | [cartera-pagos-y-contrato-de-obligaciones.md](cartera-pagos-y-contrato-de-obligaciones.md) | — |
| Ventas a crédito de Comercial | [cartera-ventas-a-credito.md](cartera-ventas-a-credito.md) | [cartera-ventas-a-credito.md](../operaciones/cartera-ventas-a-credito.md) |
| Integración contable (matriz, comprobantes, conciliación) | [cartera-integracion-contable.md](cartera-integracion-contable.md), [contabilidad-contrato-de-contabilizacion.md](contabilidad-contrato-de-contabilizacion.md) §9 | — |
| Calificación, deterioro (Anexo 1) y cierre mensual | [cartera-riesgo-y-cierre-mensual.md](cartera-riesgo-y-cierre-mensual.md) | [cartera-cierre-mensual.md](../operaciones/cartera-cierre-mensual.md) |
| Migración de la cartera vigente y activación | [plantillas-de-importacion.md](plantillas-de-importacion.md) §5a | [cartera-puesta-en-marcha.md](../operaciones/cartera-puesta-en-marcha.md), [cartera-retiro-heredado.md](../operaciones/cartera-retiro-heredado.md) |
| Plan de pagos, paz y salvo y certificado de saldo | [cartera-documentos-al-deudor.md](cartera-documentos-al-deudor.md) | — |

## K2 — libranza

| Tema | Manual | Operación |
|---|---|---|
| Pagadurías, convenios, archivo de descuentos y no aplicados | [cartera-libranza-y-pagadurias.md](cartera-libranza-y-pagadurias.md) | — |

## K3 — novedades, condonaciones y castigo

| Tema | Manual | Operación |
|---|---|---|
| Reestructuración, modificación, novación, refinanciación, condonaciones, eventos del deudor e insolvencia | [cartera-novedades-y-condonaciones.md](cartera-novedades-y-condonaciones.md) | — |
| Castigo por lote y evaluación de cartera | [cartera-castigo-y-evaluacion.md](cartera-castigo-y-evaluacion.md) | — |

## K4 — cobranza

| Tema | Manual | Operación |
|---|---|---|
| Etapas, reparto, gestiones, promesas, acuerdos, jurídico, campañas, gasto de cobranza, aviso previo y reclamos | [cartera-cobranza-y-aviso-previo.md](cartera-cobranza-y-aviso-previo.md) | [cartera-despliegue-k4.md](../operaciones/cartera-despliegue-k4.md) |

## K5 — documentos, informes y reportes regulatorios

| Tema | Manual | Operación |
|---|---|---|
| Extracto y certificado tributario | [cartera-documentos-al-deudor.md](cartera-documentos-al-deudor.md) | — |
| Informes de gestión, cosechas, concentración y tablero | [cartera-informes-y-tablero.md](cartera-informes-y-tablero.md) | — |
| SICSES y centrales de riesgo | — (manual en línea «Formato SICSES y archivos para centrales de riesgo») | [cartera-sicses-y-centrales.md](../operaciones/cartera-sicses-y-centrales.md) |

## K6a — pérdida esperada (Anexo 2)

| Tema | Manual | Operación |
|---|---|---|
| Modelo de deterioro por segmento, historial de mora, adopción y desistimiento | [cartera-perdida-esperada.md](cartera-perdida-esperada.md) | [cartera-perdida-esperada.md](../operaciones/cartera-perdida-esperada.md) |

## K6b — vivienda y UVR

| Tema | Manual | Operación |
|---|---|---|
| Ley 546, tope de vivienda, créditos en UVR y corrección monetaria | [cartera-vivienda-y-uvr.md](cartera-vivienda-y-uvr.md) | [valores-nacionales.md](../operaciones/valores-nacionales.md) §3a |

## Captaciones y aportes (feature 014)

Índice propio, con las reglas que no se negocian y **todo lo pendiente de validar por la contadora** (P2, P3, P8 a P12 y P26):
[cartera-captaciones.md](cartera-captaciones.md). Transversales a K7–K11: la integración contable
([cartera-captaciones-integracion-contable.md](cartera-captaciones-integracion-contable.md)) y la seguridad, los saldos iniciales y la activación
([cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §6–§7). Operación:
[captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md), [captaciones-despliegue.md](../operaciones/captaciones-despliegue.md)
y [captaciones-retiro-heredado.md](../operaciones/captaciones-retiro-heredado.md).

### K7 — aportes

| Tema | Manual | Operación |
|---|---|---|
| Línea de aportes, cuota, recaudo por Nómina y pagadurías, consulta de aportes, devoluciones y cesiones | [cartera-captaciones-aportes-y-retiro-del-asociado.md](cartera-captaciones-aportes-y-retiro-del-asociado.md) | [captaciones-puesta-en-marcha.md](../operaciones/captaciones-puesta-en-marcha.md) |
| Retiro del asociado: acta, liquidación, cruce, devolución | [cartera-captaciones-aportes-y-retiro-del-asociado.md](cartera-captaciones-aportes-y-retiro-del-asociado.md) §5–§8 | — |
| Catálogo común de líneas y apertura de cuentas | [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) | — |

### K8 — ahorro a la vista y permanente

| Tema | Manual | Operación |
|---|---|---|
| Movimientos por el contrato, cargos, débito automático, intereses, retención, GMF y cancelación | [cartera-captaciones-movimientos-intereses-retencion-y-gmf.md](cartera-captaciones-movimientos-intereses-retencion-y-gmf.md) | [cartera-proceso-diario.md](../operaciones/cartera-proceso-diario.md) |
| Bloqueos, embargos, límite inembargable y documentos al titular | [cartera-captaciones-bloqueos-embargos-y-documentos.md](cartera-captaciones-bloqueos-embargos-y-documentos.md) | — |

### K9 — CDAT y ahorro contractual

| Tema | Manual | Operación |
|---|---|---|
| CDAT, título, tasa negociada y ahorro contractual | [cartera-captaciones-cdat-y-contractual.md](cartera-captaciones-cdat-y-contractual.md) | — |

### K10 — asamblea, inactividad y fallecimiento

| Tema | Manual | Operación |
|---|---|---|
| Revalorización, excedentes, amortización; inactividad, no reclamados y fallecimiento | [cartera-captaciones-asamblea-inactividad-y-fallecimiento.md](cartera-captaciones-asamblea-inactividad-y-fallecimiento.md) | [valores-nacionales.md](../operaciones/valores-nacionales.md) |

### K11 — liquidez e informes

| Tema | Manual | Operación |
|---|---|---|
| Fondo y riesgo de liquidez, depósitos asegurados, informes, SICSES y exógena de captaciones | [cartera-captaciones-liquidez-e-informes.md](cartera-captaciones-liquidez-e-informes.md) | [captaciones-despliegue.md](../operaciones/captaciones-despliegue.md) |

## Transversales

- **Plantillas de importación** de Cartera (`lending.*`): [plantillas-de-importacion.md](plantillas-de-importacion.md) §5a y §5b. La carga de saldos
  de captaciones y aportes va por partes en su propia pantalla: [cartera-captaciones-lineas-y-apertura.md](cartera-captaciones-lineas-y-apertura.md) §7.
- **Plataforma de integración** —varios emisores, el proceso como origen, el registro de participantes, los canales externos y los dos caminos
  de correo—: [plataforma-de-integracion.md](plataforma-de-integracion.md) §9.
- **Manual en línea**: la ayuda de la aplicación (SISTEMA › Manual del sistema, o «¿Cómo se hace?» en cada pantalla) trae un tema por cada
  pantalla del menú en tres módulos: «Cartera» (créditos), «Captaciones y aportes» y, dentro de «Contabilidad», la matriz, los tipos de
  comprobante, la completitud y los lotes de Cartera y los indicadores regulatorios; en «Administración», el perfil regulatorio, los canales
  externos y los participantes de la integración, y en «Cumplimiento», las listas restrictivas y las solicitudes del titular. Cada tema remite a
  estos manuales.

## Decisiones del dueño que cambian lo que se ve

- **2026-10-18** (créditos, T1091–T1097): plazos y periodicidades regulatorias son **informativos** —nada se bloquea por ellos—: la evaluación de
  cartera fuera de su periodicidad avisa ([cartera-castigo-y-evaluacion.md](cartera-castigo-y-evaluacion.md)) y cada cooperativa fija la
  periodicidad del SICSES (meses de corte y días de plazo; el último día hábil descuenta los festivos,
  [cartera-sicses-y-centrales.md](../operaciones/cartera-sicses-y-centrales.md)). En las centrales, la mora bajo el rango negativo va con sus días
  reales y sin marca negativa. El certificado tributario trae aparte el anticipo recibido y la corrección monetaria UVR del año
  ([cartera-documentos-al-deudor.md](cartera-documentos-al-deudor.md)). El reparto de un pago se corrige con el hecho de redistribución también en
  la corrida de una usura ([cartera-integracion-contable.md](cartera-integracion-contable.md)). Los **requisitos de la activación se exigen en
  todos los ambientes**: fuera de producción sólo cuentan como recaudador y tesorería los dobles de prueba.
- **2026-10-21 y 2026-10-22** (captaciones): ver [cartera-captaciones-respuestas-del-dueno.md](cartera-captaciones-respuestas-del-dueno.md). Lo que
  más se nota: los avisos al titular salen por **correo electrónico real** y **SMS y WhatsApp esperan proveedor** (también en cobranza y aviso
  previo, [cartera-cobranza-y-aviso-previo.md](cartera-cobranza-y-aviso-previo.md)); la instrucción de vencimiento del contractual se puede retirar;
  la cuenta migrada se inactiva como cualquier otra; la asamblea retiene en la fuente y sube la base de los retornos por plantilla.
- **Asociados**: los **grupos del asociado** reemplazaron al centro de costo del asociado, que se retiró
  ([grupos-del-asociado.md](grupos-del-asociado.md)); el catálogo de centros de costo queda sólo como contable.

### Dos caminos de correo

Cartera escribe correos por dos caminos que no se mezclan: las **notificaciones de usuario** —alertas a quien trabaja en el ERP (valor
nacional faltante, tarea diaria que no corrió, canal externo caído…), por `COR_Alerts`/`COR_Notifications`— y los **mensajes al deudor**
—cola con plantilla versionada, autorización de tratamiento de datos, ventana de contacto de la Ley 2300 y evidencia de entrega; por ahí
salen también el envío de la simulación y de los documentos—. Ver [plataforma-de-integracion.md](plataforma-de-integracion.md) §9.7 y
[cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md) §7.

### El grupo de menú «Captaciones y aportes»

La feature 014 **retiró** lo heredado de ahorros, aportes y CDT: las 45 tablas, la API `/api/savings/*`, `/api/cdt/*` y
`/api/reports/savings/*` (responden 404, sin alias), las pantallas y los grupos de menú «Ahorros y aportes» y «CDT»
([captaciones-retiro-heredado.md](../operaciones/captaciones-retiro-heredado.md)). Las captaciones y los aportes nuevos son **parte de Cartera**,
con permisos `Lending.*` y API bajo `/api/lending`, y tienen su propio grupo de menú, **«Captaciones y aportes»**, junto al grupo «Cartera» de los
créditos. Cada enlace se muestra sólo a quien tiene su permiso:

| Subgrupo | Enlace | Ruta | Permiso |
|---|---|---|---|
| Configuración | Líneas de captación | `/cartera/lineas?naturaleza=captacion` | `Lending.DepositLines.View` |
| Configuración | Líneas de aportes | `/cartera/lineas?naturaleza=aportes` | `Lending.ContributionLines.View` |
| Cuentas | Cuentas de ahorro y aportes | `/cartera/ahorros` | `Lending.Accounts.Read` |
| Cuentas | Aportes sociales | `/cartera/aportes` | `Lending.Accounts.Read` |
| Cuentas | CDAT | `/cartera/cdat` | `Lending.Accounts.Read` |
| Cuentas | Embargos | `/cartera/embargos` | `Lending.Accounts.Read` |
| Cuentas | Retiros de asociado | `/cartera/retiros-asociado` | `Lending.MembershipWithdrawals.File`, `.Approve` o `.Settle` |
| Cuentas | Asamblea | `/cartera/asamblea` | `Lending.Assembly.Register` o `.Approve` |
| Cuentas | Fallecimientos | `/cartera/fallecimientos` | `Lending.DeceasedCases.Register` o `.Approve` |
| Cuentas | No reclamados | `/cartera/no-reclamados` | `Lending.Accounts.Read` |
| Cuentas | Fondo de liquidez | `/cartera/fondo-de-liquidez` | `Lending.LiquidityFund.View` |
| Cuentas | Reportes regulatorios | `/cartera/captaciones/reportes-regulatorios` | `Lending.RegulatoryFiles.Read` |
| Cuentas | Aprobación de movimientos | `/cartera/captaciones/aprobaciones` | `Lending.Attributions.View` |
| Puesta en marcha | Saldos iniciales | `/cartera/captaciones/migracion` | `Lending.Deposits.Migration` |
| Puesta en marcha | Activación | `/cartera/captaciones/activacion` | `Lending.Deposits.Activate` |

Las fichas (`/cartera/ahorros/{id}`, `/cartera/aportes/{persona}`, `/cartera/cdat/{id}`, `/cartera/cdat/nuevo`, `/cartera/retiros-asociado/{id}`)
se abren desde sus listas. «Líneas de crédito» sigue en el grupo «Cartera»: las tres naturalezas comparten la pantalla `/cartera/lineas`, que abre
en la naturaleza que la persona puede ver. Los informes de captaciones están en `/cartera/informes` y en la sección «Captaciones y aportes» del
centro de reportes; la matriz contable, en Contabilidad › Cartera. Las rutas heredadas que no se renovaron (`/cartera/depositos`,
`/cartera/retiros`, `/cartera/cdt…`, `/cartera/zonas`, `/cartera/parametros-ahorro`, `/cartera/parametros-sipla`…) ya no existen.

## Lo que queda abierto

Lo que depende del dueño, de la contadora o de un ambiente está en
[estado-y-pendientes.md](../operaciones/estado-y-pendientes.md) (sección de la feature 013): Recaudo y Tesorería reales (Q1), los cotejos
de layouts (SICSES y centrales), el Anexo 2, la verificación en MAUI y la promoción por entrega.

De la feature 014 (detalle en [cartera-captaciones.md](cartera-captaciones.md) y en la sección P20 de
[estado-y-pendientes.md](../operaciones/estado-y-pendientes.md)):

- **Contadora** (T235): los parámetros y valores nacionales «pendiente de validar» —retención y su umbral en el CDAT (P2), GMF y su política (P3),
  formatos SICSES y exógena (P8, P9), aportes en el certificado tributario (P10), revalorización (P11), límites de participación (P12), bonificación
  del contractual (P26)—, las cuentas CUIF sugeridas de la matriz, los casos dorados y el cotejo de los siete layouts de captaciones.
- **Abogado**: límite inembargable en las cooperativas (P5), entrega sin juicio de sucesión (P6), cuentas abandonadas y la Ley 1777 (P7), los textos
  de las respuestas al juzgado; con el oficial de cumplimiento, el umbral de origen de fondos en efectivo (P4).
- **Dueño**: las preguntas abiertas de `research.md` §13.4 a §25.1 (resumen en [cartera-captaciones.md](cartera-captaciones.md), «Lo que decide el
  dueño»).
- **Maestro**: registrar en la central `LIMITE_INEMBARGABLE`, `TOPE_ENTREGA_SIN_SUCESION` y el `IPC` de cada año, y confirmar
  `COMPONENTE_INFLACIONARIO` y `TOPE_SEGURO_DEPOSITOS` ([valores-nacionales.md](../operaciones/valores-nacionales.md)).
- **Ambiente**: Recaudo y Tesorería reales para captaciones (Q1: ninguna entrega de captaciones sale a producción sola), las e2e «Captaciones e2e»
  escritas que se corren al final, QA y MAUI a mano, y la medición con usuarios de la cooperativa piloto (T234).
- **Sin construir**: el F29 de riesgo de liquidez, el código contable del 9026, el traslado al ICETEX, la pantalla del lote de documentos, la
  pantalla para registrar y levantar bloqueos de una cuenta, y elegir en pantalla la cuenta de un tercero para los intereses de un CDAT (el sistema
  lo admite con su permiso, la pantalla de constitución todavía no). Volver a girar un pago de excedentes o una entrega por fallecimiento
  rechazados ya se construyó (E258), y las líneas de captación y de aportes ya tienen plantilla.
