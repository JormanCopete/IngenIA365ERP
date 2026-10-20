# Cartera: libranza y pagadurías

> Feature 013 (cartera financiera), fase 15 (US10, entrega K2). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §13, §17 y §20,
> [contracts/mensajes.md](../../specs/013-cartera-financiera/contracts/mensajes.md) §12.4–§12.6 y
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §6. Aquellos son
> el contrato; esto es la receta para **el jefe de cartera** y quien parametriza las pagadurías.

Una **libranza** es un crédito que la pagaduría del deudor (su empleador o su fondo de pensiones) le descuenta de nómina
y gira a la cooperativa (Ley 1527 de 2012). Cartera no recauda: arma cada período el **archivo de descuentos** de la
pagaduría y se lo entrega a **Recaudo**, que lo envía, recibe la respuesta y la devuelve. Lo descontado queda «pendiente
de giro» hasta que la pagaduría gira; lo que no se descontó queda como **no aplicado** con su motivo y su seguimiento.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Pagadurías y su convenio: alta, versiones, contactos, códigos de respuesta | `/cartera/pagadurias`, `/cartera/pagadurias/{id}` | `Lending.PayrollAgreements.View` · `.Manage` |
| Carga masiva de convenios (plantilla `lending.payroll-agreements`) | pestaña «Carga masiva» de `/cartera/pagadurias` | ídem |
| Lo descontado pendiente de giro de una pagaduría | pestaña «Pendientes de giro» del detalle | `Lending.PayrollDeductionFiles.Read` |
| Archivos de descuento: vista previa, generar, descargar, anular | `/cartera/archivos-de-descuento`, `/cartera/archivos-de-descuento/{id}` | `Lending.PayrollDeductionFiles.Read` · `.Generate` |
| No aplicados y su resolución | `/cartera/no-aplicados` | `Lending.PayrollDeductionFiles.Read` · `Lending.PayrollNotApplied.Manage` |
| Trasladar una libranza | botón «Trasladar libranza» en `/cartera/creditos/{id}` | `Lending.PayrollTransfers.Manage` |
| Formatos de ida y de vuelta | `/maestros/formatos-bancarios` (ámbitos «Libranza») | `Core.BankFileFormats.View` · `.Manage` |
| Informes `payroll-agreements` y `payroll-deduction-reconciliation` | `/cartera/informes` | `Lending.Reports.View` (+ `Lending.Loans.Read` para el cuadre) |
| Recaudador de prueba (sólo fuera de producción) | pestaña «Libranza» de `/cartera/dobles-de-prueba` | `Lending.TestDoubles.Operate` |

## 2. Antes de la primera libranza

1. **El registro como operador de libranza** en el perfil regulatorio (`/admin/perfil-regulatorio`): sin él no se crea un
   convenio ni se radica por libranza (`Lending.PayrollAgreement.OperatorRegistrationMissing`).
2. **Los límites legales replicados**: `LIBRANZA_CAPACIDAD_DESCUENTO` (la fracción del salario o pensión, después de los
   descuentos de ley, que admite la libranza), `LIBRANZA_VENTANA_GIRO_DIAS` y `LIBRANZA_UMBRAL_MORA_PAGADURIA`. Los
   registra el maestro en la central y llegan con la réplica.
3. **Los parámetros de la cooperativa** (`/cartera/parametros`): `Libranza.CapacidadDeDescuento` (vacío: rige la ley),
   `Libranza.VentanaDeGiroDias` y `Libranza.UmbralDeMoraDePagaduria`. **El umbral está sin valor hasta que el dueño lo
   coteje contra la Circular Básica Contable y Financiera** (C71): mientras esté vacío, la mora de la pagaduría **nunca**
   se imputa al deudor.
4. **La pagaduría como persona jurídica** en Maestros › Personas (con su NIT). Para la nómina propia, la pagaduría es la
   propia cooperativa (la persona con el NIT de la empresa).
5. **Los formatos** en `/maestros/formatos-bancarios`: uno de **ida** (ámbito «Libranza: archivo de descuentos») y uno de
   **vuelta** (ámbito «Libranza: respuesta de la pagaduría») por cada layout que use una pagaduría. Son genéricos (sin
   banco) y se eligen por referencia desde el convenio: dos formatos del mismo ámbito pueden tener la misma vigencia. El
   de vuelta se prueba en la pestaña «Vista previa» del editor, cargando una respuesta real de la pagaduría: muestra lo que
   se leyó por origen (crédito, código de respuesta, descontado, fecha) y los errores con su línea y campo.

## 3. El convenio

Un convenio es de una pagaduría y vive en **versiones sin cruces** (una nueva cierra la anterior la víspera). Cada versión
dice:

- las fechas de la nómina de la pagaduría: corte, envío del archivo (y su antelación) y pago;
- los formatos de ida y vuelta;
- el **tope propio** (opcional; nunca menos estricto que la ley ni que el parámetro de la cooperativa);
- si el archivo **lleva lo vencido** de períodos anteriores;
- la **ventana de giro** (días que tiene la pagaduría para girar lo descontado; nunca más que la ley);
- la **regla del no aplicado**: pasar a pago por caja, enviar a cobranza o incluir en el archivo siguiente;
- la **forma de pago alterna** (caja o débito automático) para cuando el deudor deja la pagaduría (un crédito se paga por libranza **o** por débito
  automático, nunca por las dos: ver el manual de movimientos de captaciones, §5);
- los **códigos de respuesta** de la pagaduría y su traducción: descontado, descontado en parte o no descontado con su
  motivo (licencia, incapacidad, retiro, tope, neto insuficiente, prelación…).

Los contactos son personas del maestro con su papel; la consulta muestra nombre, papel, correo y teléfono, nunca el
documento.

## 4. Radicar por libranza

La solicitud con forma de pago «libranza» exige el convenio vigente, la identificación del empleado ante la pagaduría y la
**certificación** (adjunto y fecha) con el **ingreso neto certificado**. Al radicar se mide la **capacidad de descuento**:
lo que la pagaduría ya descuenta de Cartera más la cuota nueva no puede pasar del tope más estricto (ley, cooperativa,
convenio) sobre el ingreso certificado. Si no cabe, la radicación se detiene con
`Lending.Payroll.DeductionCapacityExceeded` y lo disponible. El desembolso crea el vínculo del crédito con el convenio y
la **fecha de libranza**, que fija la prelación entre libranzas.

## 5. El archivo de descuentos

En `/cartera/archivos-de-descuento` se elige la pagaduría y el período (año, mes y, en quincenal, la quincena).

- **Vista previa**: las líneas que saldrían, en orden de fecha de libranza, con su **novedad** frente al último archivo
  enviado: alta, cambio (con el valor anterior), retiro (crédito cancelado o trasladado) o sin cambio. No guarda nada.
- **Generar**: guarda el archivo **inmutable** con su huella SHA-256 y lo entrega a Recaudo (`DescuentosPorPagaduria`).
  Antes del envío se puede **regenerar** (queda una versión nueva y la anterior «reemplazada») o **anular** con motivo.
- **Enviado**: lo marca Recaudo cuando confirma la entrega (con el radicado de la pagaduría), no esta pantalla. Desde ahí
  el período no se regenera (`Lending.PayrollFile.AlreadySent`): la corrección va como novedad del período siguiente.
- **Descargar** es un enlace firmado de 60 segundos y queda en la auditoría.

La nómina propia no tiene archivo: sigue descontando por el contrato de obligaciones como en K1; desde K2 su convenio fija
la regla del no aplicado y si la nómina lleva lo vencido.

## 6. La respuesta de la pagaduría y el pendiente de giro

Recaudo devuelve la respuesta (`ResultadoDeDescuentos`) y Cartera la aplica **todo o nada**:

- **Descontado**: la cuota queda **pendiente de giro**, con el vencimiento de la ventana y el umbral sellados ese día.
  Mientras la mora de la pagaduría (contada desde que vence su ventana) no supere el umbral, **la cuota no cuenta para la
  altura de mora del deudor** ni le causa interés de mora. Superado el umbral, la cuota vuelve a contar desde su fecha
  exigible, se avisa al deudor una sola vez y se alerta (`Cartera.GiroDePagaduriaAtrasado`).
- **No descontado** (o en parte): queda un **no aplicado** con el motivo traducido del código, el seguimiento automático
  de la regla del convenio y el aviso al deudor (`NOAPLICPAG`); se alerta por archivo (`Cartera.NoAplicadoDePagaduria`).
- Una línea que no es del archivo o con otro valor esperado rechaza la respuesta entera (`Lending.PayrollFile.LineMismatch`).

**El giro** entra por el contrato de obligaciones como un pago de la pagaduría (`ExternalPaymasterDeduction`) que cita la
línea del archivo: su fecha valor es la del descuento (aunque pase el límite de días de fecha valor), la pagaduría es el
pagador y el pendiente se levanta. Si la pagaduría tiene pendientes, un pago suyo sin la referencia se rechaza. Reversar
el giro vuelve a dejar el pendiente.

## 7. No aplicados

`/cartera/no-aplicados` lista los de las pagadurías, los de la nómina propia (desde K1) y los de la puesta en marcha, con su
seguimiento. Resolver uno abierto —pasar a caja, enviar a cobranza o cerrar— exige motivo y avisa al deudor por sus
canales autorizados. **Resolver no paga la cuota**: sigue exigible.

## 8. Traslado de una libranza

Desde el crédito, «Trasladar libranza»:

- **a otra pagaduría con convenio** (el deudor cambió de empleador): con la identificación, la certificación nueva y su
  ingreso neto; se mide la capacidad contra ella y **se conserva la fecha de libranza** original (la prelación no se
  pierde). No es una novedad del crédito.
- **sin pagaduría nueva** (retiro sin nuevo empleador): pasa a la forma de pago alterna del convenio y se avisa al
  deudor (`PASOACAJA`).

Si el traslado cambia entre «con libranza» y «sin libranza» y las condiciones contables lo distinguen, sale el hecho
`DimensionContableCambiada` en la misma transacción.

## 9. Informes

- `payroll-agreements`: por pagaduría, los créditos, lo exigible del período, lo descontado pendiente de giro, los días de
  mora de la pagaduría y si superó el umbral.
- `payroll-deduction-reconciliation` (filtros `agreement`, `year`, `month`): el cuadre línea por línea —enviado,
  descontado, pendiente de giro, aplicado, no aplicado y motivo—. Trae datos del deudor.

## 9a. Grupos de archivo, aportes y ahorros (feature 014, decisión del dueño del 2026-10-18)

Una pagaduría que separa los archivos (créditos por un lado; aportes y ahorros por otro) se configura en el convenio:
**grupos de archivo**, cada uno con su formato de ida y de vuelta, y **equivalencias**: cada código de la pagaduría dice
qué líneas de Cartera agrupa (crédito, aportes o ahorro; una línea o todas) y cómo se reparte lo que la pagaduría informa
descontado por ese código (por prioridad o a prorrata). Las dos cosas registran una versión nueva del convenio.

- **Generar** se hace por grupo (campo «Grupo de archivo» en `/cartera/archivos-de-descuento`). Van los créditos vinculados
  al convenio y las cuotas de aportes y de ahorro permanente o contractual de los asociados cuya **empresa empleadora en
  su ficha** tiene el NIT de la pagaduría. Cada línea lleva el código de la pagaduría; una obligación o cuenta que ningún
  código cubre no va en ningún archivo y la vista previa lo avisa. Si el archivo lleva cuotas de cuentas, Recaudo tiene
  que haber adoptado `DescuentosPorPagaduria` v2 en el registro de integración.
- **La respuesta** trae lo descontado por empleado y por código. Se reparte entre las líneas de ese empleado con ese
  código según la regla del código; lo que no alcanza queda como no descontado (en créditos, un no aplicado con su
  seguimiento; en cuentas, la cuota sigue exigible). Una fila de un empleado que el archivo no tiene, de un código que no
  se le pidió o lo descontado que sobra quedan en «no aplicados» de la respuesta (`GET …/payroll-deduction-files/{id}/unmatched`)
  sin detener lo demás.
- **El giro** lo aplica Recaudo por los dos contratos: los créditos como siempre y las cuotas de cuentas por el contrato de
  cuentas, nombrando la línea del archivo.

## 10. Lo que aporta el dueño

- El valor del umbral de mora de la pagaduría, cotejado contra la CBCF (T0855). Las pruebas usan 30 días.
- Los layouts de ida y vuelta de cada pagaduría y la cooperativa como persona jurídica para su nómina propia (T0856). Las
  pruebas usan formatos genéricos delimitados y el recaudador de prueba emite la respuesta.
