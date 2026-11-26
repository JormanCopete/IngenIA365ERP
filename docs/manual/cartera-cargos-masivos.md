# Cartera: cargos masivos

> Feature 026 (pedido del dueño del 2026-11-25). Especificación: `specs/026-cargos-masivos/`.

Un **cargo masivo** crea, en una sola corrida, una obligación de una línea de crédito para cada asociado escogido, **sin solicitud y sin girar
dinero**, y la contabiliza toda en **un solo comprobante**. El caso de origen es el fondo de solidaridad (`FDOSOL`): cada mes, el mismo valor a
todos los asociados activos.

Cada obligación queda como cualquier crédito de su línea: se paga en caja, se descuenta por nómina o pagaduría, sale en el estado de cuenta y
puede caer en mora. Su origen dice «Cargo masivo».

## 1. Preparar la línea (una sola vez)

En **Cartera → Líneas**, abra una versión nueva de la línea y marque **«Admite cargos masivos»**. Diga:

- el **valor por defecto** del cargo (debe estar entre el monto mínimo y el máximo de la línea);
- las **cuotas por defecto** (una, si se deja vacío);
- si sus cargos **se reportan a centrales** y si **entran a cobranza**. De fábrica, ninguna de las dos.

Una línea marcada **no puede exigir** codeudores, garantías, documentos, vínculo laboral ni consulta a centrales: el cargo no es una solicitud.
La periodicidad y la forma de pago por defecto son las de la versión.

La plantilla de líneas tiene las mismas columnas: `admiteCargoMasivo`, `valorCargoMasivo`, `cuotasCargoMasivo`, `cargoReportaACentrales` y
`cargoEntraACobranza`.

## 2. La regla contable (la contadora)

En **Contabilidad → Cartera → Matriz**, la plantilla sugiere el rol **«Contrapartida del cargo»**. Póngale la cuenta que recibe el cargo
(para `FDOSOL`, la del fondo de solidaridad). Se puede abrir por línea si hay varias.

El comprobante sale con el tipo **`CO` «Cargos masivos»** (se cambia en *Tipos de comprobante de Cartera*):

| Lado | Cuenta | Tercero |
|---|---|---|
| Débito | La cuenta por cobrar de capital de la línea (regla de saldo de siempre) | El asociado |
| Crédito | La contrapartida del cargo | El que se elija en la corrida: el mismo asociado o un proveedor |

Donde la cuenta exige tercero o documento cruce, va una línea por asociado; si no, una por cuenta, sucursal y centro de costo.

Si falta la regla y `Contabilidad.FaltaDeRegla` está en «Bloquear», la corrida **no se puede confirmar** y la vista previa dice qué falta.

## 3. La corrida, cada mes

**Cartera → Procesos e informes → Cargos masivos → Nueva corrida**.

1. **Datos**: la línea, el período (año y mes), la fecha del cargo (es la del comprobante), la fecha del primer pago, el valor, las cuotas, la
   periodicidad, la forma de pago y, si se quiere, el ciclo de pago (quincena, década o semana en que debe caer el primer pago). El tercero de
   la contrapartida es el mismo asociado o un proveedor.
   - **Forma de pago «la de cada asociado»**: la ficha con descuento por nómina va a la pagaduría de su empresa empleadora si tiene convenio, o
     a la nómina propia; cualquier otra, a caja. Si la línea no admite la que resulta, va la de defecto de la línea, con aviso.
2. **A quién**: los asociados activos, con filtros opcionales de agencia, forma y período de descuento de la ficha y empresa empleadora; o un
   **plano** (ver §4).
3. **Vista previa**: incluidos, omitidos con su motivo, total y la evaluación contable. **No crea nada.** Se recalcula cuantas veces se quiera.
4. **Confirmar**: vuelve a calcular. Si algo cambió desde la vista previa (alguien se retiró, ya tiene el cargo, cambió la línea), pide revisarla
   de nuevo. Confirmada, la corrida avanza **en segundo plano**, por tandas; si se interrumpe, sigue donde iba.

### Quién queda fuera

| Motivo | Cuándo |
|---|---|
| Retirado | Ficha en estado `R` o con fecha de retiro cumplida (salvo reingreso posterior) |
| Fallecido | Persona marcada fallecida o con un caso de fallecimiento abierto |
| Retiro en trámite | Retiro de asociado radicado, aprobado, liquidado o por rehacer |
| Ya tiene el cargo del período | Otra corrida de la misma línea y el mismo período ya se lo cargó |
| Sin pagaduría / sin convenio de nómina propia | La forma de pago lo necesita y no lo hay |

Quien está en **cobro jurídico sí se incluye**. Lo que cambie entre la confirmación y la ejecución (un retiro, por ejemplo) se vuelve a mirar
con cada asociado.

**Un asociado no recibe dos cargos de la misma línea en el mismo período.** Repetir la corrida no duplica nada.

### Segregación

Con `Segregacion.CargoMasivoPorOtraPersona` en Sí (Cartera → Parámetros), quien prepara la corrida no la puede confirmar. De fábrica, no.

## 4. El plano

Si el valor o las condiciones no son iguales para todos, cree la corrida con origen **«plano»**, baje la plantilla desde el detalle y súbala con
una fila por asociado:

| Columna | Obligatoria | Nota |
|---|---|---|
| `corrida` | Sí | El número `CGM-…` del borrador; un archivo por corrida |
| `persona` | Sí | Documento sin puntos ni dígito de verificación |
| `valor`, `cuotas`, `primerPago`, `formaDePago` | No | Vacíos = los de la corrida. Forma: Caja, NominaPropia, Pagaduria o DebitoAutomatico |

Se revisa sin guardar y se aplica todo o nada. Una persona inexistente, ambigua (dos tipos de documento con el mismo número), que no es asociada
o repetida es un error de fila. Un retirado o alguien ya cargado no es error: sale omitido en la vista previa.

## 5. Anular

- **El cargo de un asociado**: en el detalle de la corrida, «Anular» en su línea, con motivo. Sale su propio comprobante `CO` de reverso.
- **La corrida entera**: «Anular corrida», con motivo. Corre en segundo plano y deja **un solo comprobante** de reverso.

Sólo se anula una obligación **sin movimientos**: sin pagos, sin causación con valor y sin novedades. La que ya los tiene se corrige como
cualquier crédito (novedad o condonación) y queda con el motivo «Tiene movimientos». Anular libera el período: el asociado se puede volver a
cargar.

## 6. Permisos

| Permiso | Qué deja |
|---|---|
| `Lending.BulkCharges.Read` | Ver las corridas y sus asociados (tiene datos personales) |
| `Lending.BulkCharges.Run` | Preparar, recalcular, descartar, subir el plano y confirmar |
| `Lending.BulkCharges.Void` | Anular la corrida o el cargo de un asociado |

El perfil sugerido «Administrador de Cartera» trae los tres.

## 7. Lo que no hace

- No es una solicitud de crédito: no mira cupo, capacidad de pago, centrales ni documentos.
- No factura electrónicamente. Si el concepto es un ingreso que debe facturarse, va por Comercial (como los servicios de Transporte).
- No aplica a aportes ni a captaciones: tienen su propia cuota.
- No se programa sola: cada mes una persona prepara y confirma la corrida.
