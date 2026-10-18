# Cartera: índice de manuales y runbooks

> Feature 013 (Cartera financiera), fase 24 (T1072). Cartera es el módulo de créditos del sector solidario que reemplaza al heredado.
> Se construyó por **entregas**: K1 es el mínimo para salir en vivo y **no sale en producción sin Recaudo y Tesorería reales** (Q1 B); las
> demás se suman sobre K1. Este índice enlaza, por entrega, los manuales de uso (`docs/manual`) y los procedimientos de operación
> (`docs/operaciones`). Las especificaciones están en `specs/013-cartera-financiera/`.

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

## Transversales

- **Plantillas de importación** de Cartera (`lending.*`): [plantillas-de-importacion.md](plantillas-de-importacion.md) §5a y §5b.
- **Plataforma de integración** —varios emisores, el proceso como origen, el registro de participantes, los canales externos y los dos caminos
  de correo—: [plataforma-de-integracion.md](plataforma-de-integracion.md) §9.
- **Manual en línea**: el módulo «Cartera» de la ayuda de la aplicación (`ManualCatalogo.Modulos.Cartera`) trae un tema por pantalla
  principal; cada tema remite a estos manuales.

### Dos caminos de correo

Cartera escribe correos por dos caminos que no se mezclan: las **notificaciones de usuario** —alertas a quien trabaja en el ERP (valor
nacional faltante, tarea diaria que no corrió, canal externo caído…), por `COR_Alerts`/`COR_Notifications`— y los **mensajes al deudor**
—cola con plantilla versionada, autorización de tratamiento de datos, ventana de contacto de la Ley 2300 y evidencia de entrega; por ahí
salen también el envío de la simulación y de los documentos—. Ver [plataforma-de-integracion.md](plataforma-de-integracion.md) §9.7 y
[cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md) §7.

### El grupo de menú «Ahorros y aportes»

Con la Cartera nueva, el menú tiene **dos grupos**: «Cartera» (los créditos, con un enlace por permiso) y «Ahorros y aportes», que
conserva las pantallas de ahorro, depósitos, retiros, CDT, aportes, zonas y SIPLA que no son crédito. Esas pantallas conservan sus rutas
`/cartera/ahorros`, `/cartera/depositos`, `/cartera/retiros`, `/cartera/cdt…`, `/cartera/aportes`, `/cartera/zonas`… (reservadas, research
R126) y su API vive en `/api/savings`; no forman parte de la Cartera nueva ni de sus permisos `Lending.*`. Quien las busque bajo «Cartera»
las encuentra en «Ahorros y aportes».

## Lo que queda abierto

Lo que depende del dueño, de la contadora o de un ambiente está en
[estado-y-pendientes.md](../operaciones/estado-y-pendientes.md) (sección de la feature 013): Recaudo y Tesorería reales (Q1), los cotejos
de layouts (SICSES y centrales), el Anexo 2, la verificación en MAUI y la promoción por entrega.
