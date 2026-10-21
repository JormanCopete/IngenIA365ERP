# Operación: ventas a crédito con Cartera

> Feature 013 (cartera financiera), fase 10 (US6, K1). Lo funcional está en
> [docs/manual/cartera-ventas-a-credito.md](../manual/cartera-ventas-a-credito.md); aquí, qué igualar al activar y cómo se midió el POS.

## 1. Qué igualar en Comercial al activar Cartera

Con Cartera activada (`POST /api/lending/activation`, que habilita el destino `Lending` del registro de participantes), Comercial empieza a
consultar a Cartera en cuanto la consulta responde «habilitada». Para que el resto cuadre:

| Parámetro (módulo `INV`) | Valor al activar | Por qué |
|---|---|---|
| `Cartera.IntegracionHabilitadaDesde` | la fecha de activación | Mientras esté vacío, la cuenta por cobrar de la venta se sella en Contabilidad aunque Cartera responda |
| `Cartera.CuentaPorCobrarRegistradaPor` | `Cartera` | Quién lleva la cuenta por cobrar desde esa fecha (se sella en cada pago al confirmar) |
| `Cartera.ConsultaSegundos` | 5 (el defecto) | Plazo de la consulta; una respuesta tardía no cambia la venta |
| `Cartera.PoliticaSinRespuesta` (por tipo de tercero) | `Bloquear` | O `PermitirConAprobacion`: sin respuesta, la venta sigue como crédito provisional con su aprobación |

En Cartera, antes de la primera venta:

- una **línea de origen comercial** (`allowsCommercialOrigin`) por tipo de comprador, por defecto para asociados y para clientes, con su
  concepto tributario del interés; su código debe ser el que sugieren los medios de crédito de Comercial (`creditDefaults.suggestedLineCode`);
- las **equivalencias de Comercial**: la periodicidad que manda Comercial (`Monthly`, `Biweekly`, `Weekly`, `SinglePayment`) y la unidad de
  plazo (`Days`). Sin equivalencia, la venta queda rechazada y reprocesable (ver el manual, §8);
- el **cupo rotativo** de cada persona que compra a crédito, y su autorización de datos `CreditProcessing`.

La destinación `Inventory` (la fila `INV` del registro, sembrada activa) ya adopta `ObligacionCreada` y `ObligacionRechazada` v1: desde esta
fase tiene consumidor (`DestinoInventario`) y el despachador entrega las respuestas a Comercial sin configuración.

## 2. La reserva dentro de la confirmación del POS (C24, G6)

La reserva del número corre **dentro de la transacción de la confirmación** de Comercial, bajo un punto de guardado propio, con este orden
de cerrojos: filas de Inventario → `LND_Setup` compartido → `LND_DebtorControls` del deudor → numerador de Cartera → numerador de
Inventario. Dos cajas que venden a la vez al mismo deudor se ordenan en el cerrojo del deudor; la segunda ve el cupo ya descontado.

### Medición

La prueba de volumen es `ConsultasYSimulacionesPerformanceTests` (sólo con `RUN_PERF_TESTS=1`, en el motor de `DB_PROVIDER`): mil
consultas de cupo y estado con p95 &lt; 1 s (SC-008) y dos cajas confirmando a la vez 25 ventas cada una sobre cuatro deudores compartidos, sin
interbloqueos. Corre en local con Testcontainers; los números son de una máquina de desarrollo, no de producción.

| Fecha | Motor | Consultas p50 / p95 | Confirmaciones p50 / p95 (abrir, leer y cobrar) | Interbloqueos |
|---|---|---|---|---|
| 2026-10-12 | PostgreSQL | 28 ms / 31 ms (máximo 194 ms) | 300 ms / 334 ms (máximo 671 ms); 50 ventas en 7,9 s | Ninguno |
| 2026-10-12 | SQL Server | 31 ms / 34 ms (máximo 242 ms) | 373 ms / 472 ms (máximo 5.120 ms, una espera en el cerrojo del deudor); 50 ventas en 14,2 s | Ninguno |

**Decisión (G6)**: se queda la reserva dentro de la unidad de trabajo de Comercial. Si una medición en producción mostrara esperas
inaceptables en el cerrojo del deudor, la alternativa prevista es reservar en una transacción propia antes de confirmar, aceptando reservas
`Expired` sin reasignar el número.

## 3. Problemas frecuentes

| Síntoma | Causa | Qué hacer |
|---|---|---|
| La venta dice «Pendiente: Cartera aún no responde» por horas | El destino `Lending` no está habilitado o el despachador no corre | Revisar `/admin/participantes-de-integracion` (fila `CAR`) y la bandeja de integración |
| La venta queda «Pendiente» con motivo de equivalencia | Falta la equivalencia de la periodicidad o la unidad | Crearla en Cartera y reprocesar desde la bandeja |
| `Inventory.Credit.LendingBlocked` sin motivo visible | Bloqueo, mora, listas o falta de autorización de datos | El jefe de cartera revisa la persona; listas y autorización las ve cumplimiento |
| La venta se confirmó sin número | La reserva falló por algo técnico (D26) | Nada: el número llega con `ObligacionCreada` |
| Reservas vencidas en `/cartera/cupos` | Borradores abandonados después de confirmar sin descartar | Son normales; el número nunca se reasigna |
