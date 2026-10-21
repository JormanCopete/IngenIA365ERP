# Fondo de liquidez, riesgo de liquidez, depósitos asegurados, informes y reportes regulatorios

> Feature 014, US15 y US17 (entrega K11). Para la contadora, el tesorero y la gerencia. Contrato: `specs/014-ahorros-aportes-cdt/contracts/api.md`
> §15 y §18.2 y `plantillas-y-archivos.md`, con las enmiendas de `research.md` §24 y §25. La norma citada es **lectura del equipo pendiente de
> validar**. Índice y pendientes: [cartera-captaciones.md](cartera-captaciones.md).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Fondo de liquidez: posición, riesgo de liquidez, constituido, fotos | `/cartera/fondo-de-liquidez` | `Lending.LiquidityFund.View` · `.Manage` (y **alcance total**) |
| Reportes regulatorios de captaciones (SICSES y exógena) | `/cartera/captaciones/reportes-regulatorios` | `Lending.RegulatoryFiles.Read` · `Lending.RegulatoryReports.Generate` · `Lending.RegulatoryFiles.MarkUploaded` |
| Informes de gestión | `/cartera/informes` y el centro de reportes («Captaciones y aportes») | `Lending.Reports.View` · `.Export` |
| Conciliación contable | `/cartera/conciliacion-contable` | `Lending.Reconciliation.View` |
| Categoría de la entidad para el fondo; inscripción en Fogacoop | `/admin/perfil-regulatorio` | el de la 013 |

El rol sugerido «Tesorero (fondo de liquidez)» trae `Lending.LiquidityFund.*`; la gerencia (`cartera.gerente`) ve la pantalla y recibe las alertas.

## 2. El fondo de liquidez: la regla

Decreto 1068 de 2015, Libro 2, Parte 11, Título 7, con el **Decreto 976 de 2026** (rige desde el 6 de agosto de 2026). La tabla vive en
`FondoDeLiquidez.PorcentajesPorCategoria` por la **categoría** que la entidad registra en su perfil regulatorio (`*` para las demás) y nace con los
defectos del decreto:

- **Régimen general** (básica, intermedia, fondos de empleados y mutuales): **10 %** de los depósitos (a la vista, CDAT, contractual y permanente
  con retiro parcial) y **2 %** del permanente que sólo se retira al desvincularse; sobre el capital.
- **Categoría plena** (por madurez residual): 10 % hasta 30 días (y lo vencido o con redención anticipada pactada), 5 % de 31 a 540 días, 0 % más
  allá; a la vista 10 %; permanente sólo al retiro 5 %; base con **intereses causados y por causar** hasta el vencimiento. Durante la **transición**
  (hasta el 2030-02-06) rige el general y el de madurez se informa.
- Cada porcentaje es el **mayor** entre el de la entidad y su **piso legal** (límites `FONDO_LIQUIDEZ_*`).

La base se lee del **libro** (no de posiciones): cuentas de captación vivas a la fecha, aportes no; lo no reclamado no cuenta.

Ejemplos (casos dorados `liquidez-*`):

| Caso | Requerido |
|---|---|
| General: a la vista 51.234.567,89; CDAT 30.000.000; contractual 5.000.000; permanente con retiro parcial 4.000.000,05; permanente sólo al retiro 20.000.000 | 5.123.456,79 + 3.000.000 + 500.000 + 400.000,01 + 400.000 = **9.423.456,80** |
| Plena pasada la transición | CDAT y contractual por madurez residual, base con intereses |
| La cooperativa guardó 8 % para depósitos (bajo el piso del 10 %) y 3 % para el permanente sólo al retiro (sobre el 2 %) | rigen el 10 % y el 3 % |

## 3. Lo constituido y las fotos

- El tesorero registra cada inversión (`.Manage`): instrumento (cuenta bancaria, inversión a plazo, fondo, TES clase B, otro), entidad, referencia,
  monto, fecha y **soporte obligatorio**; termina cuando deja de contar.
- La **foto** de una fecha (base, requerido, constituido, diferencia, IRL) es **inmutable**: una por fecha. La toma el ciclo diario según
  `FondoDeLiquidez.Periodicidad` (`Semanal`: el domingo; `Mensual`: el último día; `Diaria`) o a mano con «Tomar la foto de la fecha».
- **Alertas**: `Cartera.FondoDeLiquidezInsuficiente` (constituido bajo el requerido), `Cartera.FondoDeLiquidezEnElMargen` (bajo el requerido ×
  (1 + `FondoDeLiquidez.MargenDeAlerta`, 5 %)), al tesorero y a la gerencia.

## 4. Riesgo de liquidez (IRL y brecha)

Modelo de la entidad sobre la Circular Básica Contable y Financiera, Título IV, Capítulo III (SARL):

- **Activos líquidos netos**: hoy **sólo el fondo constituido**, con el descuento de su clase (`RiesgoDeLiquidez.DescuentosPorInstrumento`: 0 en
  cuentas, 20 % lo demás). El disponible y las inversiones de Tesorería, las obligaciones financieras, las cuentas por pagar, los créditos aprobados
  sin desembolsar y los fondos sociales **no están** (pregunta abierta: leerlos de Contabilidad).
- **Entradas**: las cuotas de cartera por vencer × (1 − `RiesgoDeLiquidez.IndiceDeMora`).
- **Salidas**: contractuales (intereses y capital del CDAT en sus fechas, el contractual al vencimiento) y no contractuales
  (`RiesgoDeLiquidez.FraccionDeRetiroALaVista` 10 % del ahorro a la vista en la primera banda; `…FraccionMensualDeRetiroDeAportes` 1 % mensual del
  permanente y los aportes).
- **IRL** acumulado por banda (`RiesgoDeLiquidez.BandasIrl`: 1–15, 16–30, 31–60, 61–90 días) = (activos líquidos + entradas) ÷ salidas; bajo
  `RiesgoDeLiquidez.UmbralIrl` (100 %) sale `Cartera.RiesgoDeLiquidezBajo` con el umbral del plan de ajuste (110 %). La **brecha** va en
  `RiesgoDeLiquidez.BandasDeBrecha` (siete bandas, la última abierta).

## 5. Depósitos asegurados (Fogacoop)

Sólo si la entidad está inscrita. Por **titular**, hasta `TOPE_SEGURO_DEPOSITOS` (50.000.000 desde el 2024-08-19) sumando capital e interés causado de
todas sus cuentas de captación; los aportes no entran; los cotitulares no se reparten (pregunta abierta). Ejemplo (`liquidez-07`): ahorradores con
12.000.000,50, 75.000.000 y 50.000.000 → asegurados 12.000.000,50, 50.000.000 y 50.000.000 (no asegurado 25.000.000). Vista `insured-deposits`.

## 6. Informes de gestión

Vistas de `/api/reports/lending/{vista}` en `/cartera/informes`, todas con `Lending.Reports.View`; las que traen titulares exigen además
`Lending.Accounts.Read` (y, para exportarlas, `Lending.Reports.ExportPersonalData`) y respetan el alcance:

| Vista | Qué | Titulares |
|---|---|---|
| `deposit-balances` | saldos por línea, clase y oficina | no |
| `deposit-transactions` | movimientos | sí |
| `deposit-accounts-by-status` | cuentas abiertas al corte con estado y marcas (embargada, exenta, causación suspendida, datos por actualizar); el estado es el de hoy | sí |
| `depositor-concentration` | concentración por depositante | sí |
| `term-deposit-maturities` | vencimientos de CDAT | sí |
| `interest-and-withholding` | intereses y retención | sí |
| `gmf` | GMF | sí |
| `charges` | cargos | sí |
| `negotiated-rates` | tasas negociadas | sí |
| `contributions-by-member`, `installments-in-arrears` | aportes por asociado y cuotas atrasadas | sí |
| `garnishments` | embargos recibidos | sí |
| `unclaimed` | no reclamados | sí |
| `liquidity-fund` | fondo de liquidez (alcance total) | no |
| `insured-deposits` | depósitos asegurados | sí |
| `deposits-reconciliation` | conciliación contable de captaciones | no |

## 7. Reportes regulatorios

Siete formatos, cada uno con su layout como **dato versionado** (`verified = false` hasta que la contadora lo coteje: cada generación lleva la
advertencia «formato sin cotejar», nunca dentro del archivo):

| Formato | Qué | Periodicidad | Titulares |
|---|---|---|---|
| `sicses-captaciones` (9026) | una fila por cuenta de captación viva al cierre del mes (tasa, plazo, vencimiento, estado, exención de GMF, cotitulares, embargo); el código contable va **vacío** | mensual | sí |
| `sicses-cotitulares` (9068) | titular y cotitular vigentes al corte | mensual | sí |
| `sicses-aportes` (9013) | por asociado: las cuatro clases (los mínimos dentro de los ordinarios), cuota del mes, promedio y último pago | mensual | sí |
| `sicses-saldos-diarios` (169) | el capital del ahorro a la vista al cierre de cada día del mes | mensual | no |
| `sicses-fondo-liquidez` (9067) | saldos de depósitos por clase y las inversiones vigentes (intereses en 0 y «no capitaliza»; una inversión «otra» bloquea) | mensual | no |
| `exogena-1019` | cuentas de ahorro (a la vista, contractual, permanente) de quien superó el umbral (`EXOGENA_1019_UMBRAL_UVT` × UVT, por persona sumando sus cuentas), con estadísticas de saldos y movimientos | anual | sí |
| `exogena-1020` | CDAT por persona y mes, repartidos por la proporción de cada titular | anual | sí |

Mecánica (la misma de la 013, ver [cartera-sicses-y-centrales.md](../operaciones/cartera-sicses-y-centrales.md)): exige el **cierre mensual** vigente
de Cartera del mes (de diciembre en la exógena); **validar** no guarda; con bloqueantes queda una versión validada sin archivo; con advertencias hay
que reconocerlas; regenerar deja la anterior reemplazada; **marcar cargado** con radicado cierra el período. Exige alcance total. El SICSES va
delimitado por `|` con fechas `dd/MM/yyyy`; la exógena por `;` en pesos enteros, como entrada del prevalidador de la DIAN. Los plazos son
informativos. **No se generan**: el F29 (159), que es el balance completo por bandas, ni el formato 1010 de aportes, que no es de Cartera.

## 8. Pendiente de validar

La tabla del fondo contra el Diario Oficial, la categoría, los pisos, el colchón y la periodicidad; las bandas, el índice de mora, las fracciones de
retiro y los descuentos; el tope de Fogacoop; el cotejo de los siete layouts (P8, P9), los mínimos en el 9013, los intereses en el 9067, el umbral
de la 1019: [cartera-captaciones.md](cartera-captaciones.md), «Fondo y riesgo de liquidez» y «Reportes regulatorios».
