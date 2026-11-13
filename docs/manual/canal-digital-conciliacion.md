# Canal digital — conciliación diaria de los pagos en línea (feature 021, US6)

Para el analista de Recaudo. Los pagos que el asociado hace en el canal digital entran a Recaudo **en línea**, como recibos
«en línea» del convenio de la pasarela, en el momento en que la pasarela los aprueba. La conciliación del día no es otro
proceso: es la **carga del archivo de la pasarela** que ya existe en Recaudo (Convenios → archivos de pasarela) y la
conciliación del convenio contra el extracto, con una regla más: reconoce los pagos del canal por su referencia.

## El formato del reporte es un dato

El reporte diario de Avalpay se describe como un **formato de archivo** (Maestros → Formatos bancarios) con ámbito
**`PaymentGatewayFile`**, ligado a la versión del convenio de pasarela. **No va en el repositorio**: el dueño lo carga con
el reporte real que entregue Avalpay, campo a campo o pegando el JSON del formato. Si Avalpay cambia el reporte, se crea
una versión nueva del formato; no se toca código.

Campos que la conciliación necesita, por su **origen** en el formato:

| Origen | Qué es en el reporte de la pasarela | Para qué lo usa la conciliación |
|---|---|---|
| `MovementDate` (o `PaymentDate`) | Fecha del pago | Fecha valor del recibo |
| `Amount` | Valor de la transacción | Valor; debe cuadrar con el pago del canal |
| `MovementReference` | **La referencia del pago que generó el canal** (18 caracteres: código de la cooperativa, fecha, secuencia y dígito) | Reconoce el pago del canal y su recibo |
| `Outcome` | Estado de la transacción (`APROBADA`, `APPROVED`, `OK`…) | Sólo las aprobadas se aplican |
| `BankSequence` | Identificador único de la transacción en la pasarela (requestId o referencia interna) | Evita cargar el mismo pago dos veces |
| `BankReference` y `BankDate` (opcionales) | Referencia y fecha propias de la pasarela | Quedan en la línea para consulta |

Lo que no se mapea no se lee. Antes de cargar el primer reporte real, use **Revisar** (no guarda nada) para ver qué haría
cada línea.

## Qué pasa con cada línea del canal

Una línea aprobada cuya referencia tiene la forma del canal (con su dígito de control) se trata así:

| Situación | Resultado de la línea | Qué hacer |
|---|---|---|
| El canal ya avisó el pago y el ERP lo aplicó | **Aplicada**, con el recibo «en línea» que ya existía; no se aplica otra vez | Nada |
| El canal nunca avisó (el aviso se perdió o el ERP estuvo caído) y el canal tiene ese pago aprobado con el mismo valor | **Aplicada por conciliación**: se aplica una sola vez, **a nombre de quien carga el archivo**, a los mismos productos y con la misma modalidad que eligió el asociado; el aviso queda con el motivo «Aplicado por la conciliación…». Si el aviso del canal llega después, recibe la misma respuesta y no duplica nada | Nada |
| El pago del canal quedó **en revisión sin recibo** (ningún producto lo admitió) | **Partida por identificar** con el motivo «pago del canal en revisión» | Resolver la partida: aplicarla o devolverla |
| Ni el ERP ni el canal tienen un pago aprobado que cuadre con esa referencia y ese valor | **Partida por identificar** con el motivo «referencia del canal sin pago que cuadre» | Revisar con la pasarela; resolver la partida |
| La misma referencia ya llegó en otro archivo del convenio, o se repite en el archivo | **Omitida** («ya cargado») | Nada |

Las líneas que no son del canal (otras referencias del convenio) siguen la regla de siempre del convenio.

Un pago aprobado nunca se rechaza: si el canal lo aplicó con saldo a favor del asociado, el recibo y el saldo ya existen y
la alerta **«Pago en línea en revisión»** (`Canal.PagoEnRevision`) le llegó a quien resuelve saldos a favor.

## El cuadre contra el banco

Después de cargar el reporte, la **conciliación del convenio** cuadra lo aplicado contra el abono del extracto: la
diferencia por la comisión de la pasarela se reconoce según la configuración del convenio y no queda como diferencia; una
diferencia sin explicación queda como partida y el día no se marca cuadrado (FR-050, FR-051). Es el mismo flujo de los
convenios de la 015 (`docs/manual/recaudo-manual-del-analista.md`).

## Requisitos

- El ERP necesita hablar con el canal para el caso «aprobado sin aviso» (`CanalServicio:UrlInterna` y `CanalServicio:LlavePrivadaErp` configurados).
  Si no puede, esas líneas quedan como partida con su motivo y no se pierde nada.
- El destino de pago del canal debe estar ligado al **mismo convenio** que carga el reporte.
