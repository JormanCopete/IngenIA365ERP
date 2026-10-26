# Asamblea, inactividad, no reclamados y fallecimiento del titular

> Feature 014, US12 y US14 (entrega K10). Para la contadora, el jefe de captaciones y el abogado de la cooperativa. Contrato:
> `specs/014-ahorros-aportes-cdt/contracts/api.md` §12 y §13, con las enmiendas de `research.md` §22 y §23. La norma citada es **lectura del equipo
> pendiente de validar**. Índice y pendientes: [cartera-captaciones.md](cartera-captaciones.md).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Revalorización, excedentes y amortización: registrar, simular, aplicar, reversar | `/cartera/asamblea` | `Lending.Assembly.Register` · `.Approve` (y alcance total) |
| Fallecimiento del titular y entrega | `/cartera/fallecimientos` | `Lending.DeceasedCases.Register` · `.Approve` |
| Saldos no reclamados: lista, traslado a mano, pago | `/cartera/no-reclamados` | `Lending.Accounts.Read` · `Lending.Unclaimed.Manage` |
| Informe `unclaimed` | `/cartera/informes` | `Lending.Reports.View` |
| Textos de `PREINACTIV`, `INACTIVA`, `NORECLAMA` | `/cartera/plantillas-de-mensaje` | los de la 013 |

## 2. Lo que dice la norma (a validar)

- Ley 79 de 1988, art. 54: del excedente, mínimos a la reserva de protección de aportes, al fondo de educación y al de solidaridad; el remanente
  puede ir a **revalorizar aportes**, a servicios, a **retornos** a los asociados y a un **fondo de amortización**. Arts. 52 y 19: amortización en
  igualdad de condiciones y revalorización dentro de lo que fije el reglamento.
- Decreto 3081 de 1990, art. 1: la revalorización llega **hasta el IPC** del año anterior. La Circular Básica Contable y Financiera (Título I, Cap. V,
  num. 5 y 5.1) la hace con cargo al fondo, sin retroactividad y **sobre el saldo promedio día/año** de cada asociado.
- Fondos de empleados (Decreto Ley 1481 de 1989, art. 19, con la Ley 2496 de 2025): el fondo de poder adquisitivo no supera el 50 % del excedente,
  amortización hasta el 49 % de los aportes y **sin retornos**.
- Cartera **no verifica** los porcentajes mínimos de reservas y fondos: es la contabilización del acta.

## 3. El flujo de una decisión de asamblea

1. **Registrar** el borrador (`.Register`): clase (revalorización, excedentes o amortización), ejercicio **cerrado**, porcentaje o valor, fondo
   aprobado, base y destino.
2. **Simular**: base de cada asociado, lo calculado, el límite del IPC, el prorrateo al fondo, los avisos y las líneas, con su huella.
3. Llevarla al orden del día de la asamblea (asunto de acta `AssemblyDistribution`: quien registró no vota).
4. **Aplicar** (`.Approve`, **otra persona**): con el punto del acta cerrado que la aprueba, el acta firmada como soporte y la huella de la simulación que
   vio (si cambió, se vuelve a simular). Aplicar dos veces se rechaza; dos aplicadas con la misma acta y clase, también.
5. **Reversar** entera (y corregir con otra que hereda el acta). No se reversa si los excedentes se pagaron en dinero, si una orden de la
   amortización sigue viva o girada, o si una cuenta quedaría en negativo.

**Quiénes entran**: los titulares de una cuenta de aportes activa o inactiva con base positiva. Quedan fuera, con aviso: quien tiene el retiro
aprobado, liquidado o por rehacer y, en la amortización, quien tiene créditos vivos (`Asamblea.AmortizacionExcluyeDeudores`). La amortización
nunca baja a nadie de su mínimo no reducible ni al capital social bajo `Aportes.MinimoIrreducibleEntidad`.

## 4. El cálculo

- **Base** (`Asamblea.BaseDeRevalorizacion`): el **saldo promedio diario** del ejercicio (suma de los saldos de cada día ÷ 365 o 366, D13) o el saldo
  al 31 de diciembre.
- **Revalorización**: base × porcentaje, **nunca sobre el IPC** del año (`Asamblea.SerieDelIpc`, serie `ANUAL`: el valor que rige el 31 de enero
  siguiente al ejercicio; sin él, `NationalValues.Missing`). Abona aportes **revalorizados**.
- **Excedentes**: porcentaje o valor; un valor se reparte por la base con el **mayor residuo** (la suma es exacta). Destino
  (`Asamblea.DestinoDeExcedentes`): a aportes (clase `Asamblea.ClaseDeAporteDeExcedentes`, ordinarios), a una cuenta de ahorro viva del titular
  (sin ella no se aplica) o pagados por orden de giro.
- **Amortización**: devuelve aportes por orden de giro, hasta lo amortizable de cada uno.
- Si lo calculado supera el **fondo**, el fondo se reparte por lo calculado con el mayor residuo y la suma es **exactamente** el fondo.
  Redondeo `Asamblea.Redondeo` (al peso).

Ejemplos (casos dorados `asamblea-*`):

| Caso | Resultado |
|---|---|
| Revalorización del 5 % sobre el saldo promedio de 2026 con IPC del 4 % y fondo de 100.000; bases 1.252.054,79, 1.627.397,26 y 686.986,30 | se limita al 4 %: calculado 142.657; se prorratea con factor 0,700982: abonos **35.107, 45.631 y 19.262** = 100.000 |
| Revalorización del 3 % sobre el saldo al 31 de diciembre con IPC del 5,2 % | no la limita; cabe en el fondo y el resto queda en el fondo |
| Excedentes por un valor de 1.000.000 | repartido por la base, suma exacta |
| Amortización del 10 % al cierre, fondo de 5.000.000; aportes de 4.000.000, 600.000 y 500.000 | 400.000; 40.000 (limitado a lo amortizable); 0 (nada amortizable) |
| Revalorización del 5,25 % en 2028 (366 días) al centavo con fondo de 11.000,55 | prorrateo al centavo |

Hechos (comprobante `AO`, **uno masivo por decisión** con una línea por cuenta): `RevalorizacionDeAportesAplicada`, `ExcedentesDistribuidos` (el pago en
dinero va del fondo de excedentes a los retornos por pagar), `AportesAmortizados`; las órdenes salen a Tesorería. **Retención en la fuente** (E260, respuesta
89 B del dueño): sobre la revalorización y sobre los retornos se retiene con el concepto, las tarifas y la base mínima en UVT que fije la cooperativa
(`Asamblea.Retencion.Concepto`, `.TarifaRevalorizacion`, `.TarifaExcedentes`, `.BaseMinimaUvt`; sin tarifa no se retiene); la simulación muestra la
columna «Retención». **Base de los retornos** (E261): la define la asamblea; con la opción «Base subida por plantilla» se registra la decisión, se
sube la base de cada asociado con la plantilla «Base de los retornos de la asamblea» (Plantillas de Cartera) y se vuelve a simular. El asociado
retirado durante el ejercicio participa a prorrata del tiempo (E259) y los excedentes de quien no tiene cuenta de ahorro se capitalizan en sus
aportes (E257). Un pago de excedentes o una entrega por fallecimiento cuyo giro rechaza Tesorería **se vuelve a girar** y el valor sigue por pagar
al destinatario (E258).

## 5. Inactividad

- Una cuenta pasa a **inactiva** cuando lleva los **días de inactividad de su línea** (D21: 360) sin movimiento del **titular**; el abono de intereses
  y los cargos no cuentan como movimiento. **La cuenta migrada se trata igual que una creada desde el aplicativo** (E262, respuesta 69): se mide desde su último movimiento (el que trae la
  carga). El CDAT no se mide.
- **Advertencia al activar**: la primera noche tras activar, toda cuenta migrada activa con último movimiento anterior al plazo de inactividad de su línea queda
  inactiva y su titular recibe el aviso `INACTIVA`. La revisión previa de la migración de captaciones lo muestra (aviso `Lending.Migration.InactivityOnActivation`,
  con cuántas cuentas son); no bloquea. Para evitarlo, corrija la fecha del último movimiento de la carga o el plazo de la línea antes de activar.
  El plazo de **no reclamados** de una cuenta migrada ya inactiva sigue corriendo desde el corte.
- Avisos: `PREINACTIV` `Captaciones.DiasDeAvisoPrevioDeInactividad` (30) días antes y `INACTIVA` al inactivar.
- Al inactivar, los saldos se **reclasifican** al rol de capital inactivo (`CaptacionReclasificada`, comprobante `CN`); sigue causando si la línea lo
  dice; el cargo de inactividad, si existe, se causa sobre la cuenta ya inactiva.
- **Reactiva** cualquier consignación, retiro o traslado del titular (y la entrega del contractual vencido, el pago de un no reclamado por traslado y la
  entrega por fallecimiento a una cuenta), con la reclasificación de vuelta.

## 6. Saldos no reclamados

Dos fuentes: **lo devuelto y no cobrado** (la última orden de giro de una devolución, cancelación o salida de CDAT volvió rechazada, anulada o con
cheque no cobrado, sin otra orden viva ni movimiento del titular) y, **sólo si la cooperativa lo habilita** (`CuentasAbandonadas.TrasladoHabilitado`,
apagado), el saldo de una cuenta inactiva tras `CuentasAbandonadas.DiasSinMovimiento` (1.095 días como referencia de la Ley 1777 de 2016, que rige a
los establecimientos de crédito; P7). El ciclo traslada solo cuando se cumplen `Captaciones.DiasParaNoReclamados` (vacío = no traslada solo); a mano se
traslada desde la pantalla, todo o nada.

- No pasan cuentas con bloqueos o embargos, de titular con caso de fallecimiento vivo, con cargos por cobrar o sin saldo; antes se liquidan los
  intereses a la fecha. La cuenta se cancela y se avisa con `NORECLAMA`.
- El dinero sale del capital y queda en el concepto «no reclamado» de la misma cuenta: **ya no es saldo del titular** (no causa ni se retira).
- **Pagarlo** (`Lending.Unclaimed.Manage`): por giro a la cuenta bancaria del titular o por traslado a una cuenta a la vista o permanente viva suya.
  Un giro devuelto lo vuelve a «no reclamado». El GMF del pago hoy no se cobra.
- **No se construyó** el traslado al ICETEX (Decreto 953 de 2016): no aplica a las cooperativas mientras el abogado no diga otra cosa.

Hechos: `SaldoTrasladadoANoReclamados` (comprobante `MA`) y, al pagar, `DevolucionOrdenada` (`RA`) o `TrasladoEntreCuentasAplicado` (`MA`).

## 7. Fallecimiento del titular

1. **Registrar** (`.Register`): la fecha de defunción (no futura) y el **registro civil de defunción** como soporte. Bloquea **todas** las cuentas vivas
   de la persona (captación y aportes) con bloqueo total, anula los retiros en espera y, si la línea lo dice, suspende la causación; el CDAT queda a
   disposición al vencer. Un registro errado se anula antes de aprobar la entrega.
2. **Entrega**: las cuentas de captación vivas con saldo (a la vista, permanente, contractual y el CDAT vencido); los **aportes van por el retiro del
   asociado** con causa fallecimiento; el CDAT sin vencer, la cuenta embargada, con giro en curso o con cargos por cobrar quedan fuera con su nota. Tres
   rutas:
   - **beneficiarios**: el conjunto vigente en la **fecha de la defunción** de cada cuenta;
   - **sin juicio de sucesión** (EOSF art. 127, P6): personas y porcentajes del soporte, iguales en todas las cuentas, hasta el tope
     `TOPE_ENTREGA_SIN_SUCESION` y con los soportes de `Fallecimiento.SoportesDeEntregaSinSucesion`;
   - **con sucesión**: con el soporte de la sucesión.
3. **Vista previa** y **registrar la entrega** (con su huella); **aprobar** (`.Approve`, **otra persona**): liquida los intereses a la fecha y entrega
   el **saldo del día** por los porcentajes, al centavo por el mayor residuo, una operación por destinatario y cuenta. Por giro la cuenta queda en
   cancelación hasta que Tesorería gire; por traslado se cancela en el acto. Con todas las cuentas canceladas, el caso queda entregado.

Cartera **no escribe** el maestro de personas: toma por fallecido al titular que el maestro marca o que tiene un caso vivo, y avisa si el maestro todavía
no lo marca. Hechos: `DevolucionOrdenada` (`RA`) o `TrasladoEntreCuentasAplicado` (`MA`); la aprobación pasa por la validación previa contable.

## 8. Pendiente de validar

Base, IPC, redondeo, destino y clase de los excedentes, exclusión de deudores, retención de la asamblea (P11); días y efecto de la inactividad, aviso
previo, cuentas abandonadas (P7), plazo de no reclamados, tope y soportes de la entrega sin sucesión (P6), GMF de la entrega y del pago de no
reclamados, cuentas sugeridas: [cartera-captaciones.md](cartera-captaciones.md).
