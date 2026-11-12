# Quickstart — 023 Emisión electrónica asíncrona

Cómo comprobar la feature a mano y qué prueba automática cubre cada paso.

## 0. Preparación local

1. Docker Desktop encendido. Un broker local:

   ```bash
   docker run -d --name ingenia365-rabbitmq -p 5672:5672 -p 15672:15672 -p 15692:15692 -e RABBITMQ_DEFAULT_USER=erp -e RABBITMQ_DEFAULT_PASS=erp-local -e RABBITMQ_DEFAULT_VHOST=/erp rabbitmq:4.1-management-alpine
   ```

2. En `appsettings.Development.local.json` (git lo ignora): `"Mensajeria": { "Proveedor": "RabbitMq", "Host": "localhost", "Usuario": "erp", "Clave": "erp-local" }`.
   Sin esa sección la API corre con `EnProceso` (sin broker) y todo funciona igual, con un solo proceso.
3. Una cooperativa con facturación electrónica en canal `SIMULADO`, una caja abierta en el POS.

## 1. Cobro sin quedar atado al canal (US1)

| Paso | Esperado | Prueba |
|---|---|---|
| Simulado con demora 0,3 s; cobrar | Respuesta < 1 s, tirilla con CUDE y QR | e2e `CobroConCanalRapido` |
| Simulado con demora 10 s; espera 2 s; cobrar | Respuesta < 2,5 s, tirilla sin CUDE con «pendiente de validación DIAN»; minutos después validado | e2e `CobroConCanalLento` |
| Espera 0; cobrar | < 1 s, tirilla sin CUDE | unitaria `EsperaDelCobro_Cero` |
| Repetir con la misma `Idempotency-Key` | Mismo resultado, una sola venta y un solo documento | e2e existente de idempotencia del cobro |
| Reimprimir tras validar | Con CUDE, sin leyenda | e2e `ReimpresionTrasValidar` |

## 2. Cero pérdida (US2)

| Paso | Esperado | Prueba |
|---|---|---|
| `docker stop ingenia365-rabbitmq`; cobrar 5 ventas | Cobran; salud `Degraded`; el barrido las emite (≤ 15 s) | e2e `SinBrokerElBarridoEmite` |
| `docker start …` | La API reconecta sola; nuevas ventas vuelven a ir por la cola | e2e `ElBrokerVuelve` |
| Simulado «pierde la respuesta» y se reinicia el host a mitad | Al retomar, primero consulta; un solo envío al canal | e2e `CaidaEntreLlamadaYRegistro` |
| Mismo aviso publicado dos veces; dos hosts consumiendo | Una sola transmisión | e2e `AvisoDuplicadoDosReplicas` |
| Venta que falla validación | Ni orden ni aviso | unitaria `SinCommitNoSePublica` |

## 3. Tope y muertos (US3)

| Paso | Esperado | Prueba |
|---|---|---|
| `Dian.TopeDeIntentos = 3`; simulado siempre 503 | 3 intentos, `DeadAt`, alerta `Dian.DocumentoSinEnviar`, aparece en `/admin/mensajes-muertos` | e2e `TopeYMuerte` |
| Reprocesar con motivo | Intenta de inmediato; auditoría `Platform.DeadLetters.Reprocessed`; alerta atendida | e2e `ReprocesoAuditado` |
| Simulado rechaza (validación) | `Rejected`, alerta de rechazo, no muere | unitaria `RechazoNoCuentaComoTransitorio` |
| Contingencia 03 con plazo a 48 h y canal caído | No muere antes del plazo | unitaria `ContingenciaNoMuereAntesDelPlazo` |
| Usuario sin `Platform.DeadLetters.Reprocess` | 403 | e2e `ReprocesoExigePermiso` |

## 4. Estado visible, oficina, seguimiento, Contabilidad (US4–US7)

- Bandeja de documentos electrónicos: filtro «En reintento» y «Muerto»; columna «Intento n de m» y «Próximo». (manual)
- Factura en oficina con canal lento: confirma < 1 s, `PendingSend`. e2e `FacturaDeOficinaNoEspera`.
- Almacén de adjuntos caído al cobrar: el cobro responde; al volver, la representación y el canónico se guardan y el correo sale una
  vez. e2e `SeguimientoTrasAlmacenCaido`.
- Dos hosts: el mensaje a Contabilidad de una venta se procesa una sola vez. e2e `IntegracionUnaSolaVez`.

## 5. Impresión directa (US9)

- Caja con «Imprimir directo» y 2 copias, en MAUI Windows con impresora predeterminada: dos tirillas sin diálogo. **A mano** (no hay
  pruebas de dispositivo en el repo).
- En Chrome sin `--kiosk-printing`: el ERP no muestra su vista previa; el navegador muestra su diálogo. Con `--kiosk-printing`: sin
  diálogo. **A mano**.

## 6. Observabilidad (US8)

```bash
curl -s http://localhost:9464/metrics | grep erp_trabajos
```

Detener el consumo (`Mensajeria:Consumir=false`), cobrar, ver crecer `erp_trabajos_pendientes` y `erp_trabajos_edad_mas_viejo_segundos`;
reanudar y verlas bajar. Buscar en la bitácora el PublicId del documento: aparecen publicación, consumo, intento y resultado.

## 7. Clúster (DEV primero)

1. `tools/scripts/crear-secreto-rabbitmq.ps1 -Ambiente dev` (crea `erp-rabbitmq` en `erp-dev`; nunca en el repo).
2. Activar el componente `components/mensajeria` en `overlays/dev` (GitOps) y sincronizar.
3. `kubectl -n erp-dev exec erp-rabbitmq-0 -- rabbitmq-diagnostics -q check_running` y `…list_queues name type messages`.
4. Cobrar en DEV y ver la cola consumirse. Runbook: `docs/operaciones/mensajeria-rabbitmq.md`.

PDN sólo con autorización expresa del dueño, respaldo previo y el aval de la contadora anotado (tirilla sin CUDE).
