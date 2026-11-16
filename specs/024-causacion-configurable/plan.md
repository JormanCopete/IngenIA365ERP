# 024 — Causación configurable de Cartera

Pedido del dueño del 2026-11-16, sobre la rama `mejoras-estado-cuenta-recaudo`. Plan de trabajo y decisiones; las recetas de uso quedan en
`docs/manual/` al cerrar cada fase.

## Lo que pidió el dueño

1. La tasa de mora puede variar cada mes: debe regir una **tasa general**, no la de cada línea.
2. Un parámetro para contabilizar la causación **en el mismo comprobante o separada** (corriente, mora y demás rubros), para saber qué
   corresponde a cada uno.
3. Un parámetro por empresa que elija, **por separado**, si la causación es **automática o manual** para: interés corriente, interés de mora y
   otros rubros (administración, seguro, otros); y si la causación es **diaria o al inicio del ciclo** según la periodicidad.
4. Un **proceso de causación** (manual o automático) con sus parámetros de ejecución:
   - Manual al inicio del ciclo: se elige de qué fecha a qué fecha va, si es la primera o segunda quincena (en quincenal), y queda causada la
     cuota completa —capital, interés y demás rubros— lista para que el asociado la pague completa. Igual en mensual (sin elegir quincena),
     semanal, decadal o diaria.
   - Dinámico, seguro y simple, con **borrador** para validar antes de dejarlo en firme y contabilizar.
   - Se puede repetir, pero los créditos con abonos no se tocan al recalcular; con parámetros de seguridad.
   - Todas las validaciones de fechas, ciclos cerrados, etc.

## Decisiones del dueño (formularios del 2026-11-16)

| # | Pregunta | Decisión |
|---|---|---|
| 1 | Tasa de mora general | La digita la empresa con vigencia (efectiva anual), topada siempre en la usura. |
| 2 | Comprobantes separados | Un **tipo de comprobante por concepto** (p. ej. CA corriente, CM mora, CS seguro…), configurable. |
| 3 | La mora | Día a día sobre lo vencido; su contabilización automática (cada noche) o manual (por rango). |
| 4 | Ámbito de «diaria / inicio del ciclo» | Por empresa, con excepción por línea. |
| 5 | Inicio del ciclo | **Parametrizable**: reconocimiento contra **ingreso diferido** (que pasa a ingreso día a día, NIIF) o **todo a ingreso** el primer día. |
| 6 | Reproceso y seguridad | Borrador libre; poner en firme y reprocesar exige permiso, motivo y aprobación de **otra persona**; al reprocesar, los créditos con abonos posteriores quedan intactos y se informan. |
| 7 | Mora general en créditos existentes | A **todos**, desde su vigencia; no se admite vigencia con fecha pasada. Sin tasa general vigente, la de la línea. |
| 8 | Administración | **Rubro propio** (en la línea, el libro, la prelación y la contabilidad), con su causación separada del seguro. |

## Principio de diseño: el motor sigue calculando día a día

Las decisiones de la 013 (FR-062, FR-063, D8, R33) fijan que el **cálculo** de intereses es diario; así quedan exactos la usura, la mora, los
abonos y las cancelaciones, y el libro siempre es igual a la reconstrucción. Esta feature **no cambia eso**. Lo que se vuelve configurable es:

- **cuándo y cómo se contabiliza** lo causado (cada noche o por un proceso manual; un comprobante o uno por concepto), y
- **el reconocimiento al inicio del ciclo**: el primer día del período (o cuando la persona corre el proceso) se reconoce la cuota completa por
  cobrar —contra ingreso diferido o contra ingreso, según el parámetro— y la caja la ofrece como lo causado para pagar completo. Cada día la
  causación real pasa el diferido a ingreso (o, en «todo a ingreso», no vuelve a reconocer). Al vencer, cancelar o abonar se **ajusta** la
  diferencia entre lo reconocido y lo realmente causado; la mora sigue corriendo sólo desde el vencimiento.

Es una enmienda a D8 en cuanto a la **contabilización** (no al cálculo), que se anota en `specs/013-cartera-financiera/` al cerrar la fase 4.

## Parámetros nuevos (Cartera, con vigencia)

| Clave | Valores | De fábrica | Ámbito |
|---|---|---|---|
| `Mora.TasaGeneral` | EA en fracción; vacío = la de la línea | vacío | General; **sin vigencia hacia atrás** |
| `Contabilidad.CausacionSeparadaPorConcepto` | Sí / No | No (un CA como hoy) | General |
| `Causacion.ModoInteresCorriente` | Automatica / Manual | Automatica | General |
| `Causacion.ModoInteresDeMora` | Automatica / Manual | Automatica | General |
| `Causacion.ModoSeguro` | Automatica / Manual | Automatica | General |
| `Causacion.ModoAdministracion` | Automatica / Manual | Automatica | General |
| `Causacion.ModoOtrosRubros` | Automatica / Manual | Automatica | General |
| `Causacion.Momento` | Diaria / InicioDelCiclo | Diaria | General y línea |
| `Causacion.InicioDelCiclo.Reconocimiento` | IngresoDiferido / IngresoDirecto | IngresoDiferido | General |

De fábrica nada cambia frente a hoy.

## Fases (en orden; cada una compila, prueba, se sube y se documenta)

### Fase 1 — Mora general, comprobantes por concepto y modo manual de contabilización

- **Mora general**: serie con vigencia leída completa (no sólo en los extremos) y llevada a la entrada de la reconstrucción como la usura
  (`TasaDelDia`, la llave de su memoria, los cortes de ventana); topada en la usura; sin vigencia hacia atrás (validación del escritor de
  parámetros para esa clave); casos dorados.
- **Comprobantes por concepto**: con el parámetro en Sí, cada hecho de causación (ICA, MCA, SCA y los nuevos) va en su propio documento, de
  manera independiente (la clave de agrupación incluye el hecho), con el tipo que la empresa mapee en «Tipos de comprobante de Cartera»; semilla
  de los tipos sugeridos (inactivos hasta que la empresa los mapee).
- **Modo manual de contabilización por concepto**: un disparador «Manual» para el hecho de cada concepto: lo causado queda esperando y no lo
  toma ni el ciclo nocturno ni el programador de lotes; lo toma el proceso de causación (fase 3). Ámbito por tipo de hecho visible en
  `/cartera/parametros`.

**Hecha (2026-11-16, sin commit).** Lo que quedó y en qué difiere de lo de arriba:

- **Mora general**: `Mora.TasaGeneral` (decimal, admite vacío, máximo 1, sólo general). `ReglasDeParametrosDeCartera` rechaza una vigencia
  antes de hoy **o en un día ya causado** (el mayor `LND_LoanControls.LastAccruedDate`; si el ciclo de hoy ya corrió, desde mañana) con
  `Lending.Parameter.BackdatedNotAllowed` (`data.minimumValidFrom`), y más de seis decimales con `Lending.Rate.TooPrecise`. La serie entera
  sale de `ILectorDeParametros.VigenciasConocidasAsync` (nuevo: las vigencias conocidas en R) por `ReglasDeParametrosDeCartera.TasaGeneralDeMoraAsync`
  → `SeriesDeModalidad.MoraGeneral` → `EntradaDeReconstruccion.MoraGeneral` (`TasaGeneralDeMora`, regla `VigenteDeCartera`; una vacía o una
  vigencia cerrada sin sucesora = sin tasa general). En el motor: `EntradaDeTasaDelDia.MoraGeneralEA` (llave de la memoria), rama en
  `TasaDelDia.Calcular` (reemplaza la sellada, se topa igual) y cortes de ventana en sus vigencias. **Vivienda**: sus reglas no fijan la tasa
  de mora (sólo que no corre sobre intereses y el tope menor entre usura y tope de vivienda), así que la general también rige allí, con ese
  tope. Caso dorado `35-tasa-general-de-mora` (`calcular.py` con la regla propia, `mora_general_del_dia`).
- **Comprobantes por concepto**: `Contabilidad.CausacionSeparadaPorConcepto` (Sí/No, de fábrica No). Se **sella al emitir** como marca en la
  granularidad (`ClavesDeLote.MarcaPorConcepto`, `PorDia+Concepto`) para `ModoDePasoDeCartera.HechosDeCausacion` (ICA, MCA, SCA, AFV, CMU);
  `AgrupadorDeResumidosDeCartera.ClaveDe` pone entonces el hecho en la clave (`{fecha}|{tipo}|{hecho}`): un trabajo, un comprobante y una
  transacción por concepto. La vista previa del lote agrupa igual y dice el concepto (`ComprobantePropuestoDeCarteraDto.Concept`). Semilla
  `CM` y `CS` **activos** y sin mapear (no inactivos como decía el plan: el mapeo sólo admite tipos activos y la semilla vuelve a inactivar
  los que declara inactivos).
- **Modo manual**: no es un valor de `Contabilidad.DisparadorDeLote` sino los parámetros `Causacion.ModoInteresCorriente`, `.ModoInteresDeMora`,
  `.ModoSeguro` y `.ModoOtrosRubros` (Automatica/Manual; «otros rubros» = la corrección monetaria UVR hasta la fase 2), sólo generales. En
  «Manual», `ModoDePasoDeCartera` sella el hecho por lotes —aunque su modo sea en línea— con el disparador `ClavesDeLote.CausacionManual` y el
  alcance `CausacionManual:{día causado}` (`AlcanceDeCausacionManual`; las partes lo fijan en su día). No lo toman `TerminarAsync` (sólo
  `AccrualRun:`) ni `ScheduleIntegrationBatchesCommand` (sólo `HoraDiaria`); lo toma el lote manual existente y lo cuenta
  `Accounting.Period.LendingPending`. Punto para la fase 3: `Lending/Integration/CausacionPorContabilizar` (entregas y resumen por día y
  hecho). No hizo falta el ámbito por tipo de hecho en `/cartera/parametros`: las claves nuevas son generales y aparecen con su descripción.
- Pendiente: validar con la contadora la tasa general (y su efecto en vivienda), los tipos `CM`/`CS` y el caso 35; la e2e de la fase.

### Fase 2 — Administración como rubro propio

- Clase en los seguros de la línea (Seguro / Administración), concepto propio en el libro, su hecho de causación (administración causada) con
  sus roles en la matriz, su lugar en la prelación por rubros, sus columnas en el estado de cuenta y en la caja. Migración nueva y aditiva.
  Los créditos ya desembolsados siguen como están.

**Hecha (2026-11-16, sin commit).** Lo que quedó y en qué difiere de lo de arriba:

- **Línea**: `InsuranceKind` (`Insurance` 1, `Administration` 2) en `LND_CreditLineInsurances.Kind` (de fábrica seguro), en el borrador, la huella
  (sólo si es administración, así las versiones de antes conservan la suya), la entrada y la salida de la API (`kind`) y el editor («Clase»,
  «Causación» y «Agregar administración»; con administración se esconden retención, autoaseguro y vida deudores). La administración **computa
  siempre** para la usura (`CreditLineInsurance.ComputaParaUsura(kind, …)`) y no puede ser vida deudores. Lleva un tercero obligatorio como el
  seguro (normalmente la propia cooperativa), que Contabilidad no usa: no se volvió opcional para no tocar la columna ni el contrato.
- **Sellado**: el JSON `LoanTermsVersions.Insurances` lleva `kind`; lo sellado antes (sin `kind`) se lee como seguro. `CargadorDeReconstruccion`
  toma el momento del seguro del primer seguro y el de la administración (`CausacionDeAdministracion`) del primer cargo de esa clase.
- **Libro y motor**: `LoanConcept.Administration = 11` (y `ConceptoDeObligacion.Administration` en el contrato). `SeguroDeCuota`/`SeguroDelPlan`
  llevan `Clase`; `CuotaDelPlan.Seguro` es sólo de los seguros y `Administracion` la nueva cubeta (dentro de `Total` y de la nivelada, que sigue
  usando la suma). `Causacion.AdministracionExacta` y la reconstrucción la causan en su concepto; cuenta en el saldo vencido y en lo cubierto por
  un giro (con el seguro). Columnas nuevas `LND_RepaymentInstallments.Administration` y `LND_LoanDailyPositions.Administration`. Huellas del
  estado y de la decisión de pago: la administración sólo entra si la hay (lo de antes no cambia). Migración `CausacionDeAdministracion`
  (20261116150227 en PostgreSQL, 20261116150249 en SQL Server; tres columnas, aditiva, sin arrastre ajeno).
- **Prelación**: el rubro `Administracion` → `LoanConcept.Administration`; ya no tiene que ir seguido del seguro (se quitó la regla). Si la lista
  nombra sólo uno de los dos, el otro va a su lado; la lista de la línea (sin el parámetro) pone la administración justo después del seguro.
- **Contabilidad**: hecho `AdministracionCausada` (`ACA`, por partes, admite proceso), `CargoPorCobrar` contra `Ingreso` con el concepto
  `Administration` —**sin rol nuevo**: el cargo por cobrar y el ingreso ya existían y la reconstrucción del asiento (`Causacion` del constructor)
  ya los cruzaba; `CargoPorCobrar` exige sólo el concepto, como los gastos de cobranza—. El pago, el reverso, el castigo, la condonación, el
  deterioro y los traslados la llevan por su concepto (`RolDelConcepto`). `Causacion.ModoAdministracion` (Automatica/Manual) y su lugar en
  `HechosDeCausacion`. Tipo sugerido **`AD`** «Causación de administración» (activo, sin mapear): el plan decía `CD`, pero `CD` es el tipo
  retirado de la liquidación de CDT (la semilla lo mantiene inactivo y los sembrados no cambian de uso). Mapeo de fábrica `ACA → CA`. Plantilla
  de la matriz con las filas de la administración; la completitud pide el cargo por cobrar de la administración en cuanto hay saldo.
- **Pantallas**: estado de cuenta (columna `administracion` en los dos catálogos, resumen por concepto, cuotas pendientes, simulación de
  cancelación, impresión y PDF), detalle del crédito, plan del desembolso y detalle de la obligación en la caja: «Administración» aparte, sólo
  cuando la hay. En el plan resumido del contrato y en la simulación de originación va en «Otros» (el plan no tiene otros cargos).
- **Créditos ya desembolsados**: siguen como están (su administración, si la tenían, quedó sellada como seguro); no hay migración de datos.
- **Pruebas**: caso dorado 36 (`calcular.py` con la administración propia: `cargos_de_cuota`, `administracion_exacta`, el rubro y el saldo
  vencido), casos contables 58 y 59, pruebas de la línea con la clase, de la prelación con rubros separados, de la completitud, del modo manual,
  de la semilla y de los conteos (71 hechos).
- Pendiente: la columna de la clase en la plantilla de líneas (lo importado nace como seguro); la administración en UVR de la cuota
  (`InsuranceUvr` no se partió); el detalle del plan en la simulación de una novedad sigue sin columna de administración; validar con la
  contadora las cuentas, si la administración se suspende con la causación y si su cargo por cobrar debe ir por categoría; la e2e de la fase.

### Fase 3 — Proceso de causación (manual) con borrador

- Pantalla `/cartera/causacion`: periodicidad, rango (propuesto por el período; en quincenal, primera o segunda quincena), conceptos (los que
  estén en modo manual), filtros opcionales (línea, oficina, pagaduría).
- **Calcular → borrador**: por crédito y concepto lo que se causa o reconoce, totales, y lo que queda fuera con su motivo (abonos posteriores,
  período contable cerrado, mes de Cartera cerrado, falta de regla contable, falta de usura…). Se recalcula cuantas veces se quiera.
- **Poner en firme**: permiso propio, motivo y **aprobación de otra persona** (plataforma de aprobaciones existente); emite los hechos y ordena
  el lote contable (uno o varios comprobantes según el parámetro).
- **Reprocesar en firme**: mismo control; los créditos con abonos posteriores no se tocan y se listan; el resto se ajusta por diferencia.
- Validaciones: Cartera activada, rango dentro de períodos contables abiertos y meses de Cartera sin cerrar, sin solaparse con otro proceso en
  firme del mismo concepto y período, fecha no futura en diaria, usura disponible, mora general vigente si aplica.

**Hecha (2026-11-16, sin commit).** Lo que quedó y en qué difiere de lo de arriba:

- **Modelo**: `LND_AccrualProcesses` (cabecera con clase, periodicidad, quincena o década, rango de días causados, conceptos como bandera,
  filtros de línea, oficina y pagaduría, estado `Draft`/`PendingApproval`/`Confirmed`/`Reprocessed`/`Voided`, huella y bloqueos del último cálculo,
  motivo, solicitud de aprobación, aprobador y método, lote y número de lote, reproceso de/por), `LND_AccrualProcessFacts` (los hechos del borrador:
  mensaje, tipo, día causado, fecha contable, sucursal, créditos, valor, estado, motivo y si bloquea) y `LND_AccrualProcessLines` (`bigint`: crédito,
  tipo de hecho, concepto del libro, valor, lo que contabilizó el reprocesado, estado y motivo). Migración aditiva `ProcesoDeCausacion` (20261116161933
  en PostgreSQL, 20261116162002 en SQL Server; tres tablas, sin arrastre ajeno). Número `PCA-` por `NumeradorDeCartera` (`LendingNumberKind.AccrualProcess`).
- **Servidor** (`Application/Lending/AccrualProcesses`): `ProcesosDeCausacion` (lo común: alcance, cruces, cerrojo, bloqueos de todas las clases,
  aplicar el cálculo, poner en firme y el DTO), `ContabilizacionDeLoCausado` (la clase `Posting`), `EntradaDeProcesoDeCausacion`,
  `FuenteDeAprobacionDeCausacion` y los comandos y consultas, todos con validador (el espacio entra en `PrincipioVIII_DualValidation`). Rutas
  `/api/lending/accrual-processes` (`options`, lista, detalle, `lines`, armar, `recalculate`, `void`, `confirm`, `reprocess`,
  `approvals/{id}/presence-challenge` y `/decide`) con `Lending.AccrualProcesses.Read`, `.Calculate`, `.Confirm` y `.Reprocess` (los siembra la API: 167
  `Lending.*`; plantillas «Jefe de cartera» con los cuatro y «Gerente» con `Read` y `Confirm` para aprobar). Toda escritura con `Idempotency-Key`; motivo
  en la auditoría del comando y el resumen de la firma en `Lending.AccrualProcess.Confirmed`.
- **Cuatro ojos** por la plataforma de aprobaciones: sujeto `LendingAccrualProcess` (regla fija de un nivel con `Lending.AccrualProcesses.Confirm`),
  fuente `LendingAccrualProcess`. Aprueba otra **persona** (el motor excluye a quien pidió, a quien armó y a quien recalculó por última vez; la fuente
  compara personas), **presente** con passkey o TOTP; desde la bandeja con su propia sesión, no. La última aprobación recalcula y compara la huella, toma
  el cerrojo y ordena el lote en la misma transacción; si algo cambió, la aprobación no queda (`Lending.AccrualProcess.Stale`).
- **El lote**: no se usó `OrderIntegrationBatchCommand` (toma todo lo del rango): se ordena un lote manual de la plataforma (`LotesDeIntegracion.Nuevo`/
  `Asignar`, a nombre de quien pidió la firma) con exactamente las entregas de los hechos incluidos; los comprobantes salen por día y, con la causación
  separada por concepto, por concepto (marca sellada en cada entrega). Los comprobantes y rechazos se leen de las entregas, nunca de `ACC_`.
- **El hecho no se parte** (diferencia con «filtros por crédito»): lo causado de un día, una sucursal y un concepto es un solo mensaje y una sola parte
  (`UK_LND_AccountingFactParts_Batch`), y el libro nombra ese mensaje. La oficina filtra exacto; con filtro de línea o pagaduría, o con créditos fuera del
  alcance o con abonos posteriores, el hecho entero queda fuera (`OutOfFilter`, `OutOfScope`, `LaterPayment`) y sus demás créditos dicen `SharedFact`.
  En la clase de la fase 4, que emite sus propios hechos, esos filtros serán exactos.
- **Motivos**: no bloquean mes contable cerrado o inexistente, contabilidad sin iniciar, mes ya cerrado por Cartera, fuera de alcance o filtro y abonos
  posteriores; bloquean la falta de regla con `Contabilidad.FaltaDeRegla = Bloquear` y la falta de respuesta con `PoliticaSinRespuesta = Bloquear` (se
  pregunta una vez por combinación de tipo, sucursal y dimensiones de las partes). Bloqueos del proceso: la causación del motor atrasada respecto al fin
  del rango (con cuántos créditos) y la usura que falta en esos días por modalidad. **Crédito castigado o cancelado no excluye** en esta clase: lo causado
  antes ya está en el libro (el castigado causa en suspenso) y su comprobante tiene que salir; esa exclusión es de la clase de reconocimiento (fase 4).
- **Reproceso**: un proceso nuevo con lo mismo (no se cambia), que toma lo que todavía espera en el rango —lo que el reprocesado dejó fuera y ya se
  corrigió, y lo causado que llegó después—: es la diferencia entre lo causado en el libro y lo contabilizado. Lo contabilizado no se vuelve a
  contabilizar ni se reversa; las correcciones por fecha valor o valor nacional viajan con su pago o su corrida. Los créditos con pagos registrados
  después de la firma del reprocesado quedan intactos y se listan; cada línea dice lo contabilizado antes. Puesto en firme, el reprocesado queda
  `Reprocessed`. Su firma exige además `Lending.AccrualProcesses.Reprocess`.
- **Validaciones**: Cartera activada, rango de la periodicidad (no futuro, largo máximo, dentro de un mes salvo la semana), conceptos en «Manual» o con
  algo esperando, filtros en el alcance (sin alcance total, la oficina o la línea son obligatorias), un proceso abierto por concepto, ninguno en firme que
  se cruce en concepto, rango y filtros (salvo el reprocesado); la «mora general vigente si aplica» no bloquea (sin tasa general rige la de la línea, D7).
- **Pantalla** `/cartera/causacion` (menú de Cartera, `Lending.AccrualProcesses.Read`) con lo que espera por concepto, la lista, el borrador (totales
  por concepto y sucursal, hechos con motivo, créditos con filtro y página), la aprobación presente (`AprobacionPresenteDeCausacion`) y el lote con
  sus comprobantes; tema `cartera-proceso-de-causacion` del manual en línea. Receta: `docs/manual/cartera-causacion-y-liquidacion.md` §9.5.
- **Punto de extensión para la fase 4**: `AccrualProcessKind` (hoy sólo `Posting`) e `IClaseDeProcesoDeCausacion` (calcular → hechos y líneas; poner en
  firme → lote). `PeriodRecognition` será otra clase registrada en DI: su cálculo escribe en `LND_AccrualProcessLines` lo que reconoce por crédito (con
  `MessagePublicId` nulo en sus hechos) y su puesta en firme emite los hechos y ordena el lote; cabecera, borrador, cuatro ojos, alcance, cruces y
  reproceso no cambian.
- Pendiente: validar con la contadora el orden «contabilizar la causación antes de cerrar el mes de Cartera»; la e2e de la fase; la prueba a mano de la
  aprobación presente en web y MAUI.

### Fase 4 — Reconocimiento al inicio del ciclo

- Hecho nuevo «cuota reconocida del período» (interés, seguro, administración y otros de la cuota), automático el primer día del período o por
  el proceso manual de la fase 3; contra ingreso diferido o contra ingreso según el parámetro.
- La causación diaria de esos créditos pasa el diferido a ingreso (o no vuelve a reconocer); ajuste al vencer, cancelar o abonar (con
  `Pagos.InteresesAlPagarAntesDelVencimiento`: con «corrido» se devuelve lo no corrido; con «cuota completa» no).
- La caja y el estado de cuenta muestran la cuota reconocida como lo causado para pagar completo; la mora corre desde el vencimiento; el pago
  de esa cuota no pide el aviso de «anticipo que no abona a capital».
- Enmienda de D8 en los artefactos de la 013.

**Hecha (2026-11-16, sin commit).** Lo que quedó y en qué difiere de lo de arriba:

- **Parámetros**: `Causacion.Momento` (`Diaria` de fábrica / `InicioDelCiclo`; general y por línea) y `Causacion.InicioDelCiclo.Reconocimiento`
  (`IngresoDiferido` de fábrica / `IngresoDirecto`; sólo general), los dos «pendiente de validar por la contadora». Los lee
  `LectorDePoliticaDeReconocimiento` (qué líneas reconocen, contra qué, y qué rubros son automáticos o manuales según `Causacion.Modo*`).
- **Modelo del libro**: seis conceptos nuevos que **sellan el modo** (`RecognizedInterest`, `RecognizedInsurance`, `RecognizedAdministration` = 12–14,
  contra el diferido; `…Direct` = 15–17, directo) y dos clases (`PeriodRecognition` = 22, `RecognitionRelease` = 23). Lo reconocido entra a la
  reconstrucción como una entrada más (`EntradaDeReconstruccion.Reconocimientos`, cargada del libro con lo conocido en R) y la reconstrucción
  produce el consumo diario (filas `Accrual` negativas en el concepto reconocido), la devolución al vencer y la del cierre por una operación;
  `EstadoDelCredito.Reconocidos` da lo pendiente por cuota y rubro. **No es saldo**: no toca los saldos, el valor para cancelar ni la mora. Cálculo
  de lo que se reconoce: `ReconstruccionDeCredito.CuotaPorReconocer` (la causación proyectada desde el día del reconocimiento hasta el vencimiento,
  con usura, tope y mora general congelados en ese día). Sin migración de datos.
- **Cuándo**: el ciclo diario reconoce el día siguiente al inicio del período de la cuota (`PuestaAlDiaDeCredito.Causar`, los rubros en
  «Automatica»), en la misma transacción; con el rubro en «Manual», la clase de proceso `PeriodRecognition` (`ReconocimientoDelPeriodo`) desde el
  día siguiente al último causado. No reconoce UVR, castigados ni suspendidos, cuotas ya vencidas o reconocidas.
- **Contabilidad**: hecho `CuotaReconocida` con código corto **`CUR`** (no `CRC`: ya era de Tesorería, `ChequesReclasificadosAlCierre`), por lotes,
  `FinDeCausacion`, por partes y `AdmiteProceso`; rol nuevo `IngresoDiferidoDeLaCuota` (crédito; exige modalidad y concepto —interés, seguro,
  administración—; tercero el deudor, cruce el pagaré; 27 roles de crédito). Lo reconocido usa la regla de la cuenta por cobrar de su rubro. El
  consumo y la devolución al vencer viajan en `InteresCausado`/`SeguroCausado`/`AdministracionCausada` del día; la devolución por una cancelación,
  en el `AjusteDeCausacionPorFechaValor` del pago. Comprobante sugerido **`CR`** «Cuota reconocida del período» (activo, sin mapear; mientras tanto
  `CA`). La plantilla sugiere las filas del rol por modalidad y rubro, y la completitud las pide cuando alguna línea reconoce contra el diferido. La
  conciliación suma lo reconocido por causar a cada cuenta por cobrar y compara el diferido.
- **Proceso** (fase 3, clase nueva): el rango es el ciclo (puede terminar después de hoy, tiene que haber empezado), los conceptos sólo los
  reconocibles (`Lending.AccrualProcess.ConceptNotRecognizable`), y cada línea lleva la cuota y el «desde» (`LND_AccrualProcessLines.InstallmentNumber`,
  `RecognitionDate`: migración aditiva **`ReconocimientoAlInicioDelCiclo`**, `20261116174125` en SQL Server y `20261116174212` en PostgreSQL). Motivos
  nuevos de exclusión (ya reconocida, ciclo sin empezar, suspendida, castigado, no activo, UVR, línea diaria). En firme: se recalcula (`Stale`), se
  escribe el libro en una corrida `PeriodRecognition`, se emiten los hechos por día y oficina y se ordena el lote, en una transacción. Pantalla
  `/cartera/causacion` con el selector de clase.
- **Caja y estado de cuenta**: la obligación lleva `RecognizedPending` y la cuota `Recognized` (detalle en la caja con la columna «Reconocido»); al
  elegir la persona y con F4 la cuota del ciclo reconocida entra completa como exigible si no hay nada vencido; el aviso «el anticipo no abona a
  capital» ya no sale para la cuota reconocida (el capital se abona al vencer, como cualquier anticipo de la cuota); estado de cuenta con la columna
  «Cuota reconocida por causar» y `LoanStatementDto.RecognizedPending`.
- **Difiere del plan**: «otros de la cuota» no se reconocen (los gastos de cobranza y otros cargos no son de la cuota; la corrección monetaria UVR
  queda fuera porque los créditos en UVR no reconocen). La devolución que causa una operación que no es pago (condonación total, novación,
  refinanciación, saldo a favor que cancela) queda en el libro pero no en su hecho: la conciliación la muestra. La reversión de un desembolso, la
  reclasificación por calificación y el cambio de dimensiones no trasladan lo reconocido. Si el parámetro entra a mitad de período, ese período sigue
  diario salvo que se reconozca con el proceso.
- **Pruebas**: casos dorados de Cartera 37 (diferido, con variantes abono a capital, pago anticipado y de fábrica «Diaria») y 38 (directo, con
  cancelación «corrido» y «cuota completa»), verificados con `calcular.py`; `ReconocimientoAlInicioDelCicloTests` (dominio); casos contables 60 a 63
  (reconocimiento contra el diferido y directo, consumo diario, devolución al cancelar); `ReconocimientoEnElCicloDiarioTests` y el reconocimiento por
  el proceso en `ProcesoDeCausacionTests` (aplicación). Enmienda de D8 en `specs/013-cartera-financiera/spec.md`. Receta:
  `docs/manual/cartera-causacion-y-liquidacion.md` §9.6.
- Pendiente: validar con la contadora el esquema contable de las dos variantes, las cuentas del diferido y los casos 37, 38 y 60–63; la e2e de la
  fase; la prueba a mano de la caja y del proceso en web y MAUI.

## Fuera de alcance

Cambiar el cálculo diario del motor; novación y refinanciación; contabilizar un comprobante mensual único (sigue uno por día y concepto, o por
proceso manual).
