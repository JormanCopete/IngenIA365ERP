# Contrato de mensajes de la bandeja — 020

Este documento complementa los contratos de mensajes de las features 012, 013 y 015. Lo que no
nombra, no cambia.

## 1. Destinos opcionales (cambio de plataforma, R12)

`TipoDeMensaje` gana `DestinosOpcionales: IReadOnlySet<string>`. Al emitir, `EmisorDeMensajes`:

1. Crea las entregas de `Destinos` como hoy: una por contenido, con la misma resolución.
2. Además, por cada destino de `DestinosOpcionales` con un participante en `IRegistroDeIntegracion`
   habilitado y vigente en la fecha de emisión, crea **una entrega más** del mismo mensaje
   (`Mode = Always`, `Pending`, `NextAttemptAt = now`).
3. Un destino opcional **sin participante no deja rastro**: ni entrega `NotApplicable` ni alerta.

**Invariantes**:
- El índice único `(MessageId, Destination)` ya garantiza una entrega por destino.
- Las dependencias de mensaje (`IntegrationMessageDependency`) se crean sólo para la entrega
  obligatoria.
- Los destinos opcionales no esperan dependencias: consumen hechos ya confirmados.

**Mensajes que declaran `TRN` como opcional**:

| Tipo | Origen | Destino obligatorio | Uso en Transporte |
|---|---|---|---|
| `PagoAplicado` v1 | CAR | CNT | asignación al cargo de la obligación (`Principal` aplicado; `PaidOff`) |
| `PagoReversado` v1 | CAR | CNT | asignación negativa; reabre el cargo |
| `ReciboEmitido` v1 | REC | CNT | aplicaciones `CommercialReceivable`, repartidas por prioridad (ruta sin Cartera) |
| `ReciboAnulado` v1 | REC | CNT | reversa de las asignaciones de ese recibo |
| `DocumentoAnulado` v1 | INV | CNT | anula los cargos abiertos de la factura |

## 2. Mensaje nuevo: `SolicitudDeConector` v1

- **Origen**: `TRN`.
- **Destino**: `CON`.
- **Clave de evento**: `Cargo:{chargePublicId:N}` (forma `ConfirmacionPorPublicId`).
- **AdmiteProceso**: sí.

```json
{
  "connectorPublicId": "…",
  "connectorVersion": 3,
  "externalId": "5f0c…",
  "originRef": "TRN:9b1e…",
  "variables": {
    "vehiculo.placa": "ABC123",
    "vehiculo.numeroInterno": "0009",
    "vehiculo.radioMovil": "123",
    "servicio.codigo": "RADIO",
    "servicio.nombre": "Radio",
    "periodo.inicio": "2026-12-01",
    "periodo.fin": "2026-12-31",
    "periodo.texto": "diciembre 2026",
    "pago.valor": 50000,
    "pago.recibo": "RC-000123",
    "pago.fecha": "2026-12-05",
    "factura.numero": "FS-000456",
    "propietario.documento": "…",
    "propietario.nombre": "…"
  },
  "sensitive": ["propietario.documento"]
}
```

**Reglas**:
- Las variables viajan **resueltas** al momento del pago, y la versión queda sellada. Un cambio
  posterior del vehículo o del conector no altera un envío ya emitido.
- `sensitive` dice qué variables no se escriben en `COR_ConnectorCalls.RequestBodySanitized` ni en
  la auditoría.
- No hay credenciales en el mensaje: el consumidor las toma de `COR_ConnectorCredentials`.

## 3. Consumidores nuevos

### `DestinoTransporte` (`Destino = "TRN"`)

**Acepta**: `PagoAplicado` v1, `PagoReversado` v1, `ReciboEmitido` v1, `ReciboAnulado` v1 y
`DocumentoAnulado` v1.

**Resultados**:
- Un mensaje que no toca ningún cargo responde `AlreadyProcessed` y no escribe nada.
- Toca uno o más cargos → `ApplyChargeAllocationsCommand`, en una sola unidad de trabajo:
  1. escribe las asignaciones (idempotentes por `(ChargeId, SourceMessagePublicId)`);
  2. recalcula el estado de cada cargo;
  3. emite `SolicitudDeConector` para los que pasan a `Paid` por primera vez;
  4. marca `PaymentVoided` en el envío de los que se reabren después de notificar.

### `DestinoConectores` (`Destino = "CON"`)

**Acepta**: `SolicitudDeConector` v1.

**Pasos** (R18):
1. Si el conector está deshabilitado o sin credenciales: `Retry` y envío `OnHold`.
2. Si `IntentosLeidos ≥ maxAttempts`: `Rejected` con `Conector.ReintentosAgotados` y envío `Review`.
3. Si no:
   - autentica (token en caché) y llama;
   - traduce por el mapa de respuestas;
   - registra el intento (`COR_ConnectorCalls`) y el estado del envío con
     `RegisterConnectorCallCommand`, en un ámbito propio, como `RegisterDeliveryResultCommand`.

**Resultados**:
- `Done` → `Processed`, con `ResultReference = externalId`.
- `DoneWithWarning` → `Processed` con aviso.
- `Retry` → `Retry`.
- `ManualReview` → `Rejected`.

## 4. Participantes del registro de integración

`IProvisionadorDeModulo`, al habilitar `TRN`, registra estos participantes en la cooperativa:

| Código | Clase | Rol | Destino | Adopta |
|---|---|---|---|---|
| `TRN` | Transport | MessageDestination | `TRN` | los cinco mensajes del §1 |
| `CON` | Connectors | MessageDestination | `CON` | `SolicitudDeConector` v1 |

Deshabilitar el módulo **cierra** la vigencia del participante `TRN` (es un hecho, no se borra).
`CON` sigue mientras haya conectores habilitados.
