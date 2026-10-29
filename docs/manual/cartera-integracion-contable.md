# Cartera: integración contable

> Feature 013 (cartera financiera), fase 11 (US7, entrega K1). Complementa
> [contracts/contabilidad.md](../../specs/013-cartera-financiera/contracts/contabilidad.md) (el contrato contable de
> Cartera, enmienda E-01 de la 009), [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §16.3 y §23 y
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) (qué trae cada hecho). Aquellos son el
> contrato; esto es la receta para **la contadora** (y para el jefe de cartera que la acompaña en la conciliación). La
> mecánica para el equipo está en [contabilidad-contrato-de-contabilizacion.md](contabilidad-contrato-de-contabilizacion.md) §9.

Cartera **no conoce cuentas** y **no escribe asientos**. Cada operación (un desembolso, un pago, la causación del día, el
cierre del mes) deja uno o varios **hechos** —qué pasó, a qué crédito, con qué dimensiones y por qué valores, por
concepto— y Contabilidad los convierte en comprobantes con **su** parametrización: la **matriz de Cartera**. La
contadora decide todas las cuentas; si la matriz no tiene la regla, el hecho no se pierde: queda rechazado en la bandeja
de Cartera hasta que se cree la regla y se reprocese.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| La matriz: reglas por rol, versiones, desactivar, plantilla, «¿a qué cuenta va?» | `/contabilidad/cartera/matriz` | `Accounting.LendingRules.View` · `.Manage` |
| Tipo de comprobante y documento cruce por hecho | `/contabilidad/cartera/tipos-de-comprobante` | ídem |
| Lo que falta en la matriz a una fecha | `/contabilidad/cartera/completitud` | `Accounting.LendingRules.View` |
| Lotes de Cartera (vista previa, lote manual) y su detalle | `/contabilidad/cartera/lotes`, `/contabilidad/cartera/lotes/{id}` | `Accounting.LendingBatches.View` · `.Run` |
| Patrimonio técnico y relación de solvencia | `/contabilidad/indicadores-regulatorios` | `Accounting.RegulatoryIndicators.View` · `.Manage` |
| Estado de Cartera del mes al cerrar | `/contabilidad/periodos` | los de períodos de la 009 |
| Conciliación contable de Cartera y lista de hechos contables | `/cartera/conciliacion-contable` | `Lending.Reconciliation.View` · `Lending.Messages.Read` |
| Hechos emitidos, rechazos, reproceso y envío de lo que no pasaba | pestaña «Emitidos» de `/cartera/bandeja-de-mensajes` | `Lending.Messages.Read` · `.Reprocess` · `.SendNotApplicable` |

La plantilla de rol `cartera.contador` trae los seis permisos contables de Cartera; `cartera.auditor`, los tres de
consulta.

## 2. Roles y dimensiones

Una regla dice: para **este hecho** (o cualquiera, `*`), **este rol** y **estas dimensiones**, la cuenta es **ésta**,
vigente desde tal fecha. Los roles son fijos (26); las dimensiones son los códigos de Cartera.

| Familia | Roles | Naturaleza |
|---|---|---|
| Cartera e intereses | `Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado`, `CargoPorCobrar` | débito |
| Deterioro | `DeterioroIndividual`, `DeterioroGeneral` | crédito |
| Orden y castigada | `SuspensoEnOrden` (débito) con su contra `ContraOrdenContingente`; `CastigadaEnOrden` (débito) con su contra `ContraOrdenControl` | — |
| Pasivos con el deudor y terceros | `Anticipo`, `SaldoAFavor`, `SeguroPorPagar`, `CuentaPorPagarATercero` | crédito |
| Recaudo y puentes | `RecaudoPorAplicar` (uno por clase de recaudador), `DesembolsoPorGirar` | débito · crédito |
| Desembolso y Comercial | `PuenteComercial`, `CuentaPorCobrarComercial` | crédito |
| Resultados | `Ingreso`, `GastoDeterioro`, `RecuperacionDeterioro`, `GastoDeCastigoOCondonacion`, `IngresoPorRecuperacion` | según el rol |
| Diferido | `IngresoDiferido` | crédito |
| Contingentes | `ContingenteAcreedora` (crédito) con su contra `ContraContingenteAcreedora` (débito) | — |

**Exigidas y opcionales.** Cada rol tiene dimensiones **exigidas**, que se comparan por igualdad y nunca van en `*`
(por ejemplo, `Capital` exige modalidad y categoría; `RecaudoPorAplicar`, la clase de recaudador), y dimensiones
**opcionales**, que casan por igualdad o con `*` y suman **peso**:

| Dimensión opcional | Peso |
|---|---|
| Línea de crédito | 512 |
| Empleado (el deudor es empleado de la cooperativa) | 256 |
| Gracia | 128 |
| Sub-segmento (con o sin libranza…) | 64 |
| Reestructurado | 32 |
| Por cuenta del asociado | 16 |
| Sucursal | 4 |
| Hecho — **sólo en los roles de resultado** | 2 |

Gana la regla vigente de **mayor peso** a la fecha contable del hecho. Como los pesos son potencias de dos, nunca hay
empate. Ejemplos: «empleado» (256) le gana a «sub-segmento» (64), por eso el capital de una deudora que es empleada va a
la cuenta de créditos a empleados aunque tenga libranza; y una regla por línea (512) le gana a cualquier combinación de
las demás.

Tres reglas que no se negocian:

- **En los roles de saldo, orden, pasivo, puente, diferido y contingentes el hecho va siempre en `*`.** Un saldo vive en
  una sola cuenta, lo mueva el desembolso, el pago o el cierre; si no, ningún auxiliar cuadraría con Cartera. Sólo en
  los roles de resultado se puede fijar el hecho (peso 2), y casi nunca hace falta: el concepto y el motivo ya separan.
- **El centro de costo se asigna, no pesa.** Cartera no conoce centros de costo. Si la cuenta maneja centro, la línea
  lleva el de la regla ganadora; si la cuenta lo **exige** y la regla no lo fija, el hecho se rechaza y se corrige en la
  matriz.
- **La categoría es la contable**: la del último cierre de Cartera (o la del nacimiento, para un crédito nuevo), nunca la
  calificación diaria. Sólo la cambian el cierre (reclasificación) y su reapertura.

## 3. Diligenciar la matriz desde la plantilla

1. En `/contabilidad/cartera/matriz`, **Descargar plantilla**. La hoja «Datos» trae las reglas vigentes (con su cuenta)
   y **filas sugeridas sin cuenta**: los roles por modalidad y categoría de las líneas activas, con la estructura del
   CUIF sembrado, marcadas «pendiente de validar» en `notas`. No se siembra ninguna regla: la matriz empieza vacía.
   Otra hoja explica cada columna.
2. Columnas (versión 2 de la plantilla; un archivo de la versión 1 se sigue leyendo y las columnas nuevas quedan en `*`):
   `operacion` (hecho o vacío = `*`), `rol`, `linea`, `modalidad`, `subsegmento`, `categoria`, `concepto`,
   `empleado`, `gracia`, `porCuentaDelAsociado`, `reestructurado` (estas cuatro con `S`, `N` o vacío = cualquiera),
   `claseDeRecaudador`, `motivo`, **las cinco de Captaciones** (abajo), `sucursal`, `centroDeCosto`, `cuenta`,
   `cuentaSugerida`, `vigenteDesde`, `vigenteHasta`, `notas`.

   Las columnas de **Captaciones** (ahorros, aportes y CDAT; contrato de la 014, contabilidad.md §1.1, §1.2 y §4.2). Pesan sólo
   por igualdad (no suman peso) y se exigen en los roles que las nombran; en los demás van vacías:

   | Columna | Valores | Se exige en |
   |---|---|---|
   | `naturaleza` | `Deposit` (ahorro) o `Contribution` (aporte); `Placement` no se admite en los roles de Captaciones | `DevolucionPorPagar` (junto con `motivo`), `NoReclamadosPorPagar` |
   | `claseDeCaptacion` | `Demand` (a la vista), `Contractual`, `Permanent`, `TermDeposit` (CDAT) | `CapitalDeCaptacion`, `CapitalDeCaptacionInactiva`, `InteresPorPagar`, `GastoPorIntereses`, `RetencionPorPagar`, `GmfPorPagar`, `GastoPorBonificacion`, `IngresoPorPenalidad` |
   | `plazoDeEmision` | `UnderSixMonths`, `SixToTwelve`, `TwelveToEighteen`, `EighteenOrMore` | sólo `CapitalDeCaptacion` y `CapitalDeCaptacionInactiva` de la clase `TermDeposit` o `Contractual` |
   | `claseDeAporte` | `Minimum`, `Ordinary`, `Extraordinary`, `Revalued` | `AportesSociales` |
   | `marcaDeCuenta` | `Standard`, `GmfExempt`, `Garnished` (manda el embargo, después la exención; la inactividad cambia el rol, no la marca) | sólo `CapitalDeCaptacion` |

   `cuentaSugerida` es informativa: la cuenta del CUIF que la fila sugerida propone; el importador la ignora. En Captaciones,
   `concepto` admite además los conceptos de la cuenta, `motivo` el tipo de orden en `DevolucionPorPagar` (por ejemplo
   `TermDepositInterest`, `MembershipRefund`) y la clase de cargo en `IngresoPorCargos`, y `claseDeRecaudador` admite `Treasury`.
   Los roles de Captaciones van con sus códigos en español (`CapitalDeCaptacion`, `InteresPorPagar`, `AportesSociales`…); sus filas
   sugeridas, en `docs/manual/cartera-captaciones-integracion-contable.md` §2.
3. Por cada fila sugerida, escriba la **cuenta auxiliar** (de movimiento, activa y habilitada para Cartera) y en
   `notas` quién lo decidió y por qué. Una fila que vuelve sin cuenta se ignora. Puede abrir una fila sugerida en
   varias (por línea, por empleado, por sucursal) o dejar dimensiones en `*` para que una sola regla cubra más.
4. Agregue las reglas que la plantilla no puede sugerir: un `RecaudoPorAplicar` por cada clase de recaudador registrada
   (`Receipts`, `Payroll`, `Contributions`) y siempre `OwnLoanPayoff` (la recogida de un crédito al desembolsar otro,
   que conviene llevar a la misma cuenta que `DesembolsoPorGirar`: así el neto es lo que queda por girar);
   `DesembolsoPorGirar` por motivo (`Transfer`, `Check`, `ThirdPartyPayment`); los pasivos, los resultados y, si se
   decide que pasen, los contingentes. De Captaciones, las que decide la contadora: `RecaudoPorAplicar` de la clase `Treasury`,
   `DevolucionPorPagar`, `EmbargosPorConsignar`, `IngresoPorCargos`, `CargoDeCuentaPorCobrar`, `IngresoPorPenalidad` y `PerdidaAbsorbida`.
5. **Revisar** (`mode=review`): no guarda nada y devuelve cada error con fila y columna. **Aplicar**: todo o nada, con
   las mismas validaciones que crear una regla a mano. Una fila con una clave que ya existe y una fecha posterior es una
   **versión nueva** y cierra la anterior la víspera.
6. Abra `/contabilidad/cartera/completitud` a la fecha de hoy y déjela vacía: combinaciones en uso sin regla,
   recaudadores sin puente, reglas cuya cuenta dejó de ser elegible y hechos sin tipo de comprobante. Los **avisos**
   (puente cuya cuenta exige documento cruce; cuenta de la causación resumida que exige tercero o cruce, que obliga a
   una línea por crédito y por día; naturaleza de la cuenta distinta de la del rol) se guardan igual: léalos y decida.
7. Revise `/contabilidad/cartera/tipos-de-comprobante`: la semilla trae los mapeos sin validar; confirme o cambie cada
   uno (sólo tipos de uso «módulo» de Cartera).

Para dudar menos: **«¿A qué cuenta va?»** en la matriz recibe hecho, rol, dimensiones y fecha, y responde la regla que
gana, su peso y las demás candidatas.

Cambiar una cuenta ya usada es **agregar una versión** con fecha posterior al último hecho contabilizado con esa regla
(antes, `Accounting.LendingRule.RetroactiveOverPosted` dice la fecha y el comprobante). Una regla **nueva** sí puede
empezar antes si no cambia nada ya contabilizado: así se rescatan hechos rechazados por regla faltante. Desactivar pide
motivo; nada se borra. Lo ya contabilizado con una cuenta equivocada se corrige con un comprobante manual `CG` de
reclasificación.

**Antes de activar Cartera** la regla `RecaudoPorAplicar` de la clase `Payroll` es obligatoria, y el concepto de nómina
`DESC_CARTERA` necesita sus cuentas: débito salarios por pagar, crédito el mismo puente «recaudos por nómina para
Cartera» (enmienda D-08 de la 010).

## 4. Tipos de comprobante

| Tipo | Lo que lleva | Documento cruce |
|---|---|---|
| `DS` | Desembolsos, su reverso y los ajustes que llegan de Comercial | `PG` + número del crédito |
| `RC` | Pagos, reversos de pago, anticipos, saldos a favor, cobro de suspenso, recuperación de castigada, cobros provisionales incorporados | `PG`; `FV` + factura en los roles de Comercial |
| `DN` | Lo que recauda Nómina (pago, reverso y recuperación de castigada con clase `Payroll`) | `PG` |
| `CA` | Causación de interés, mora y seguro, ajustes por fecha valor, gasto de cobranza | `PG` |
| `PV` | Deterioro, reclasificación por calificación, reverso de cierre y castigo | `PG` |
| `CN` (nuevo) | Reestructuración, modificación, novación, condonación, cambios de dimensión y contingentes | `PG` |

El cruce sólo aparece en las líneas cuya cuenta lo exige. Los puentes de recaudo y de desembolso conviene que **no**
lo exijan: el otro lado contabiliza con su propio documento y no saldarían.

## 5. Asientos de ejemplo

Con **roles**, no cuentas: la cuenta de cada línea la pone la matriz. Crédito de ejemplo: consumo con libranza,
categoría A, sucursal Principal. Las cifras completas, línea por línea, están en `contracts/contabilidad.md` §24; los
mismos quince son casos dorados que las pruebas automáticas verifican.

| # | Hecho (comprobante) | Débito | Crédito |
|---|---|---|---|
| 1 | Desembolso por transferencia (`DS`) que recoge otro crédito | `Capital` | `DesembolsoPorGirar` (`Transfer`); la recogida es un pago (`RC`): `RecaudoPorAplicar` (`OwnLoanPayoff`) contra `Capital` e `InteresPorCobrar` del crédito recogido |
| 2 | Causación del día (`CA` resumido) | `InteresPorCobrar` (una línea por crédito si la cuenta exige tercero y cruce) | `Ingreso` (sumado) |
| 3 | Pago por caja con anticipo (`RC`) | `RecaudoPorAplicar` (`Receipts`) | `Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado` (seguro) y `Anticipo` |
| 4 | Anticipo aplicado al vencer la cuota (`RC`) | `Anticipo` | los conceptos de la cuota, con la prelación |
| 5 | Reverso de un pago después de un cierre que pasó el crédito de A a C (`RC`, fechado en el primer mes abierto) | `Capital`, `InteresPorCobrar`, `PagoPorCuentaDelAsociado` **en C** y `Anticipo` | `RecaudoPorAplicar` |
| 6 | Descuento de la nómina propia (`DN`) | `RecaudoPorAplicar` (`Payroll`) | `Capital`, `InteresPorCobrar`. Nómina, en su `NM`: salarios por pagar contra el mismo puente |
| 7 | Cierre de noviembre: el crédito pasa de A a C (`PV` resumido) | cada saldo en C; `DeterioroIndividual` en A; `GastoDeterioro` (`Individual`) por el aumento | el mismo saldo en A; `DeterioroIndividual` en C (traslado y aumento) |
| 8 | Deterioro que baja en otro ejercicio (`PV`) | `DeterioroIndividual` | `RecuperacionDeterioro` (si el gasto fuera del mismo ejercicio, `GastoDeterioro`) |
| 9 | Interés en suspenso: su causación (`CA`) y su cobro (`RC`) | causación: `SuspensoEnOrden`. Cobro: `RecaudoPorAplicar` y `ContraOrdenContingente` | causación: `ContraOrdenContingente`. Cobro: `Ingreso` (concepto en suspenso) y `SuspensoEnOrden` |
| 10 | Crédito nacido de una venta de Comercial (`DS` en especie) | `Capital` | `CuentaPorCobrarComercial` (venta sellada «Contabilidad») o `PuenteComercial` (sellada «Cartera»), con cruce `FV` + factura |
| 11 | Cobro provisional incorporado al activar (`RC`) | `CuentaPorCobrarComercial` (`FV` + factura) | `Capital`; la cuenta por cobrar provisional queda en cero por factura |
| 12 | La deudora pasa a ser empleada (`CN` resumido) | cada saldo con empleado **sí**; `DeterioroIndividual` con empleado **no** | el mismo saldo con empleado **no**; `DeterioroIndividual` con empleado **sí** |
| 13 | Condonación de mora (`CN`, K3) | `DeterioroIndividual` hasta su saldo y `GastoDeCastigoOCondonacion` (`Waiver`) por el resto | `InteresPorCobrar` (mora) |
| 14 | Castigo (`PV`, K3) y recuperación (`RC`) | castigo: `DeterioroIndividual`; `CastigadaEnOrden`; `ContraOrdenContingente` por el suspenso. Recuperación: `RecaudoPorAplicar` y `ContraOrdenControl` | castigo: `Capital` e `InteresPorCobrar`; `ContraOrdenControl`; el suspenso sale de `SuspensoEnOrden` y entra a `CastigadaEnOrden`. Recuperación: `IngresoPorRecuperacion` y `CastigadaEnOrden` |
| 15 | Corrección de usura de un mes ya pagado (todo en el `CA` de la corrida, desde el 2026-10-18) | ajuste: `Ingreso` (lo causado de más). Reparto del pago (`PagoRedistribuidoPorValorNacional`): `InteresPorCobrar` (el interés que el pago ya no cubre) | ajuste: `InteresPorCobrar`. Reparto: `Capital` (lo que el pago cubre de más) o `SaldoAFavor`/`Anticipo` (lo que deja de más) |

**Capitalización en una reestructuración** (`CN`, K3; no es uno de los quince): lo **causado** que se capitaliza va a
`Capital` contra `InteresPorCobrar`; lo **suspendido**, a `Capital` contra `IngresoDiferido`, y además se **cancela su
orden** (débito `ContraOrdenContingente`, crédito `SuspensoEnOrden`), porque ese interés deja de estar en suspenso al
volverse capital. Esta cancelación es una propuesta **pendiente de validar** (§10).

**Pagos posteriores redistribuidos** (decisión del dueño del 2026-10-18): un pago con fecha valor anterior, o el reverso de un
pago, puede cambiar lo que cubren los pagos que vinieron después. Esa reasignación va en el **mismo comprobante** del pago o del
reverso, con el molde del reparto de la fila 15: lo que el pago posterior cubre de menos vuelve al débito de su cuenta de saldo,
lo que cubre de más sale al crédito, y el anticipo o saldo a favor que deja de más va al crédito de su pasivo. No toca el puente
del recaudo, porque lo recibido no cambia. Hasta esa fecha la reasignación no se asentaba y quedaba como diferencia en la
conciliación. Lo mismo vale, desde la misma fecha, para un **ajuste de Comercial** (nota crédito, devolución, nota débito o
anulación) fechado antes de un pago ya aplicado: la reasignación de ese pago va en el mismo comprobante `DS` del ajuste.

Dos efectos que conviene tener presentes: un reverso lleva las dimensiones **vigentes** del saldo que restituye (ejemplo
5), nunca el espejo del original; y si la contadora lleva dos categorías o los dos sub-segmentos a la misma auxiliar, la
reclasificación entre ellas no produce líneas (el recibo dice «mismas cuentas»).

## 6. Cuando un hecho no se contabiliza

**Antes de confirmar.** Cartera pregunta a Contabilidad si lo que va a emitir es contabilizable. Con
`Contabilidad.FaltaDeRegla = Advertir` todo se confirma, queda marcado como no contabilizable y la contadora recibe la
alerta `Cartera.HechoNoContabilizable` (una por combinación y día). Con `Bloquear` —**el defecto**, decisión del dueño del
2026-10-14— **todo se detiene** («Bloquear todo»), cada operación a su manera:

| Dónde | Con «Bloquear» y sin regla |
|---|---|
| Pantallas de Cartera (el desembolso; en K3, novedad, condonación y castigo) | no se confirma; el mensaje dice qué falta y quién lo corrige |
| Pagos por el contrato (Recaudo, Aportes, la recogida, el recaudador de prueba) | el pago se **rechaza** con `Lending.Prevalidation.NotPostable` y lo que falta; no se escribe nada de él |
| Nómina (ordinaria y definitiva) | la aprobación **entera falla** con `Payroll.LoanDeductionNotPostable`, que nombra el crédito y la regla que falta; nada queda aprobado |
| Ventas a crédito de Comercial | la obligación no nace: el mensaje queda **rechazado y reprocesable** en la bandeja de Cartera (la venta ya está confirmada en Comercial); corregida la matriz, se reprocesa |
| Procesos (causación diaria, corrección por valor nacional, cierre mensual) | la tanda **no avanza**: la corrida queda fallida con `Lending.Prevalidation.NotPostable` y sale la alerta; corregida la matriz, «Reintentar» la retoma |

Fuera de las pantallas el bloqueo también levanta la alerta, porque quien opera no corrige la matriz. **Sin contabilidad
iniciada, o con el hecho en «no pasa», no se pregunta nada y nada se bloquea.** Sin respuesta de Contabilidad sólo la
pantalla puede bloquear (`Contabilidad.PoliticaSinRespuesta`). Como el defecto es `Bloquear`, una nómina o una tanda
esperan a la matriz: hay que completarla (`/contabilidad/cartera/completitud`) **antes de iniciar la contabilidad con
Cartera activa**, o fijar `Advertir` con su vigencia en `/cartera/parametros` mientras se completa.

**Después de confirmar.** Si Contabilidad rechaza un hecho, la operación **no se deshace**: el hecho queda «rechazado» en
la pestaña «Emitidos» de `/cartera/bandeja-de-mensajes`, con el motivo y quién lo corrige, y la alerta
`Integracion.MensajeRechazado`. Se corrige la causa y se **reprocesa** desde la misma bandeja (selección y motivo); el
comprobante sale con la **fecha contable original**.

| Motivo | Quién corrige | Dónde |
|---|---|---|
| Falta la regla (`Accounting.LendingRule.Missing`: dice hecho, rol, dimensiones, fecha y créditos) | Contabilidad | `/contabilidad/cartera/matriz` |
| Tipo de comprobante sin mapeo, inactivo o de otro módulo; documento cruce inválido | Contabilidad | `/contabilidad/cartera/tipos-de-comprobante` |
| Cuenta que no es de movimiento, inactiva, no habilitada para Cartera, que no admite centro o pide base gravable | Contabilidad | `/contabilidad/plan-de-cuentas` |
| La cuenta exige centro, documento cruce o tercero y no lo hay | Contabilidad | la matriz (el centro en la regla) o el plan de cuentas (quitar la exigencia) |
| El tercero no es válido | Core | Personas |
| Período cerrado | Contabilidad | `/contabilidad/periodos` (reabrir y reprocesar) |
| Contabilidad sin iniciar | Contabilidad | `/contabilidad/configuracion` |
| El hecho no cuadra o trae más de dos decimales | Cartera | soporte: es un defecto de quien lo emitió |

Un mes que todavía no existe en Contabilidad (el 1 de enero sin abrir el ejercicio) **no rechaza**: el hecho espera y
la alerta `Contabilidad.PeriodoInexistente` pide abrirlo. Un reverso cuyo pago original todavía no se contabilizó
espera a que lo esté.

**Lo que no pasaba.** Si un tipo de hecho estaba en «no pasa» (los contingentes por defecto, o todo mientras la
contabilidad no estaba iniciada), primero se cambia su modo en los parámetros de Cartera y después «Enviar lo que no
pasaba» en la misma bandeja; cada hecho llega con su fecha contable original.

## 7. Conciliación

`/cartera/conciliacion-contable?date=` compara, a una fecha, el saldo de Cartera (sus transacciones por **fecha
contable**) con el saldo de las cuentas que la matriz resuelve para cada combinación, **por conjuntos de cuentas**: si
llevó varias categorías a una auxiliar, o una combinación tiene reglas por sucursal, se comparan juntas. Familias:
capital; intereses; pagos por cuenta y cargos; deterioro individual, general y adicional; anticipos y saldos a favor;
suspenso y castigada; los puentes (que deben quedar en cero; en `DesembolsoPorGirar`, las órdenes de giro emitidas que
Tesorería no ha girado) y, si pasan, los contingentes.

Cada diferencia se explica **hecho por hecho** con lo pendiente, en lote, rechazado o que no pasa. Con todo procesado,
la diferencia es **cero**. En los puentes, lo que los hechos de Cartera no explican es **del otro lado**: el recaudador o
Tesorería no han contabilizado lo suyo. Una diferencia sin explicar levanta `Cartera.DescuadreContable` a la contadora
y al jefe de cartera; búsquela en un `CG` manual sobre una cuenta de la matriz o en una regla cambiada con efecto
sobre lo ya contabilizado. La pestaña «Hechos» lista los hechos contables con su estado y su comprobante, y el
detalle de una parte (la causación, el cierre) muestra la lista por crédito.

Se presenta en cada cierre mensual al último día del mes, después de procesar el lote del cierre.

## 8. Cierre de mes con Cartera

1. Cartera causa al día y cierra el mes (`/cartera/cierres`): reclasifica por categoría, calcula el deterioro en delta y
   emite sus hechos fechados el último día del mes, que debe estar **abierto** en Contabilidad.
2. El lote del cierre produce un `PV` resumido. Revise `/contabilidad/cartera/lotes` y la conciliación.
3. En `/contabilidad/periodos`, al cerrar el mes:
   - **Bloqueo** (`Accounting.Period.LendingNotClosed`): con Cartera activada, Contabilidad **no cierra** un mes que
     Cartera no cerró (o cuyo cierre se reabrió). No hay forma de saltarlo: primero se cierra Cartera.
   - **Aviso** (`Accounting.Period.LendingPending`): hay hechos de Cartera del mes pendientes, en lote o rechazados.
     «Procesar ahora» ordena un lote manual; «Cerrar de todos modos» queda auditado, y lo que siga pendiente se
     rechazará por período cerrado (se recupera reabriendo el mes y reprocesando).
   - **Aviso** (`Accounting.Period.LendingLoadWillExpire`): hay una carga de Cartera aplicada sin activar y cerrar el mes
     impediría activarla.
4. Reabrir un mes en Contabilidad no reabre el de Cartera. Cartera sólo reabre su último cierre, con motivo, y **antes**
   del cierre contable del mes; su reverso llega como comprobante nuevo (`ReversoDeCierre`), nunca como reversión.

Sin Cartera activada, Contabilidad cierra como siempre (sólo pueden saltar los dos avisos).

## 9. Indicadores regulatorios

Cartera necesita de Contabilidad, para el arrastre, el deterioro y el límite de concentración:

| Indicador | De dónde sale |
|---|---|
| Resultado del ejercicio | Clases 4 a 7 del ejercicio, sin el comprobante de cierre `CI` |
| Pérdidas acumuladas | Cuentas de la clase 3 de naturaleza débito |
| Patrimonio técnico · relación de solvencia | **Los registra la contadora** en `/contabilidad/indicadores-regulatorios` con su corte, valor, fuente y **soporte** adjunto (el formato o el certificado); no se calculan |

Contabilidad entrega el último registrado con corte hasta la fecha; Cartera lo da por vigente durante
`Contabilidad.IndicadoresVigenciaMeses` (6 meses). Corregir un indicador es registrar otro **del mismo tipo y corte**
indicando cuál reemplaza; un segundo registro sin decir cuál reemplaza se rechaza. **Sin patrimonio técnico vigente**,
Cartera no aplica la excepción de aportes del arrastre ni el descuento de aportes en el deterioro, y **bloquea** el
desembolso, la asignación de cupo y la novedad que aumenten el endeudamiento («patrimonio técnico no disponible»),
con alerta a la contadora. Si la solvencia no aplica a la cooperativa, no se registra.

## 10. Pendiente de validar por la contadora

Lo que el diseño adopta con la propuesta por defecto y espera su visto bueno (el deterioro y el castigo se completan en las fases siguientes de la feature) (preguntas de contabilidad de
`decisiones-transversales.md` §5 y decisiones de §7):

| Punto | Qué se propuso | Dónde se nota |
|---|---|---|
| **C2** | Cuando el deterioro baja, primero contra el gasto del mismo ejercicio (`DecreaseSameYear`) y el resto a recuperaciones (`DecreasePriorYears`); el ejercicio es el año calendario. Parámetro `Contabilidad.DisminucionDeDeterioro` (`MismoEjercicioPrimero` por defecto o `TodoARecuperacion`) | Ejemplos 7 y 8 |
| **C4** | La dimensión «reestructurado» en la matriz y la reclasificación de **todo** saldo con categoría al cerrar (capital, intereses, pagos por cuenta, suspenso y deterioro individual), no sólo capital e intereses | Ejemplo 7 y la conciliación por categoría |
| **C6** | El comprobante nuevo `CN` para las novedades y `PG` («pagaré») como documento cruce también en los créditos nacidos de Comercial, que no tienen pagaré; si lo objeta, la alternativa es un cruce nuevo `CR` «Crédito de cartera» (cambia la semilla, no el contrato). Los mapeos sembrados quedan sin validar | `/contabilidad/cartera/tipos-de-comprobante` |
| **C7** | Los roles `IngresoDiferido` (capitalización de lo suspendido al reestructurar) y `GastoDeCastigoOCondonacion` (castigo con deterioro insuficiente y condonación), sin cuenta sembrada | Ejemplos 13 y 14; capitalización |
| **C8** | La dimensión «deudor empleado» sale del estado **actual** de la persona; cuando cambia, el ciclo diario emite el traslado de saldos | Ejemplo 12 |
| **C10** (pregunta) | Las comisiones y costos de originación **no** se difieren (`Contabilidad.DiferirComisiones` = no); si se decide diferirlos, harán falta un hecho de amortización y otra entrega | — |
| **C77** | El hecho va en `*` en los roles de saldo, orden, pasivo, puente, diferido y contingentes (peso 2 sólo en resultados), y el centro de costo se asigna sin pesar | §2 |
| **C81** | Al castigar, el interés en suspenso sale de `SuspensoEnOrden` y entra a `CastigadaEnOrden` (contra `ContraOrdenControl`) | Ejemplo 14 |
| Fallecimiento (K3, decisión del dueño del 2026-10-15) | Si el parámetro `Causacion.ContinuaTrasFallecimiento` de la línea dice que no, desde el fallecimiento la causación pasa a suspenso y sale `CausacionSuspendida` con el motivo **`Deceased`** (el interés va a la orden contingente como en la mora). El motivo se queda; falta confirmar que ese tratamiento contable es el que corresponde | `CausacionSuspendida`, motivo `Deceased`; [cartera-novedades-y-condonaciones](cartera-novedades-y-condonaciones.md) §8 |
| Capitalización (propuesta del 2026-10-13) | Al capitalizar interés **en suspenso** en una reestructuración, modificación o novación, además de llevarlo a `Capital` contra `IngresoDiferido`, se **cancela su orden**: débito `ContraOrdenContingente`, crédito `SuspensoEnOrden` | §5, capitalización |

También quedan a su criterio, sin ser preguntas: la orientación de cuentas del CUIF de las filas sugeridas, si las
cuentas de interés exigen tercero y cruce (una línea por crédito y por día en la causación resumida; con muchos
créditos conviene la granularidad por día y sucursal o quitar la exigencia) y las cuentas de `DESC_CARTERA` en Nómina.
