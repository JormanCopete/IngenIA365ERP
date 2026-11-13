# Emisión electrónica asíncrona y mensajes muertos

Feature 023 (2026-11). Para cajeros, supervisores y administradores de la cooperativa.

## 1. Qué cambió en el cobro del POS

- El cobro ya **no espera** a que la DIAN valide. En cuanto la venta queda confirmada (número, kardex y recaudo), el ERP espera a lo sumo
  unos segundos —el parámetro **`Dian.EsperaMaximaPosSegundos`** del punto de venta, **2 s** por defecto, 0 = no esperar— y entrega
  **siempre** la tirilla.
- Si la DIAN validó dentro de esa espera, la tirilla sale **completa**, con el código único (CUDE o CUFE) y el QR.
- Si no alcanzó, la tirilla sale **igual**, **sin código único ni QR**, con la leyenda «Documento electrónico pendiente de validación DIAN».
  La emisión sigue detrás, sola. Cuando el documento queda validado, el comprador recibe por correo la representación completa, y una
  reimpresión ya sale con el código.
- El código único **lo genera el canal** (el proveedor tecnológico o el servicio central), no el ERP: por eso una tirilla entregada antes de
  validar no puede llevarlo.
- La factura confirmada desde **Ventas en oficina** tampoco espera: queda «Pendiente de envío» y se emite detrás.

> Antes de pasarlo a producción, la contadora debe avalar la entrega de la tirilla antes de la validación (queda anotado en el runbook).

## 2. Estados que verá

En la venta, en la factura y en **Ventas › Documentos electrónicos**:

| Estado | Qué significa |
|---|---|
| Pendiente de envío | Confirmada; todavía nadie la llevó al canal. |
| Enviado | El canal la recibió; falta la respuesta de la DIAN. |
| En reintento | El canal no respondió; se vuelve a intentar sola (intento n y próxima hora). |
| Aceptado / con notificaciones | La DIAN la validó. |
| Rechazado | La DIAN la rechazó: se corrige, se reemplaza o se cancela como siempre. **No** pasa a mensajes muertos. |
| Muerto | Agotó el tope de intentos: nadie la intenta sola. Hay que reprocesarla. |
| En contingencia | Emitida en contingencia; se transmite cuando la contingencia cierra (el plazo es informativo). |

La bandeja tiene los filtros **«En reintento»** y **«Muertos»**.

## 3. Tope de intentos

- **`Dian.TopeDeIntentos`** (12 por defecto): intentos seguidos con el canal caído o sin respuesta antes de que el documento muera. Con las
  esperas actuales (15 s, 1, 2, 5, 15, 30 y 60 min, luego cada hora) son unas 7 horas. Vale también para el **seguimiento** (guardar la
  representación y mandar el correo).
- **`Integracion.TopeDeIntentos`** (12 por defecto): lo mismo para los mensajes de la bandeja de integración (por ejemplo, a Contabilidad).
- Una respuesta definitiva (validado, rechazado) reinicia la cuenta. Un documento en contingencia no muere antes de su plazo de transmisión.
- Al morir se levanta la alerta **`Dian.DocumentoSinEnviar`**, **`Dian.SeguimientoDetenido`** o **`Integracion.EntregaDetenida`**.

Los dos topes se cambian en **Administración › Parámetros**, con vigencia, como los demás.

## 4. Mensajes muertos (Administración › Mensajes muertos)

- Lista lo que agotó su tope: documentos electrónicos, seguimientos y mensajes de integración, con la historia de intentos y el último motivo.
- **Reprocesar**: marque uno o varios, escriba el motivo (10 a 500 caracteres) y confirme. Se intentan de inmediato con la cuenta en cero;
  el motivo queda en la auditoría y la alerta se atiende. Reprocese **cuando la causa esté resuelta** (el canal responde, el almacén de
  adjuntos está arriba, el destino acepta el mensaje); si no, volverá a morir.
- Permisos: **`Platform.DeadLetters.View`** para ver (el administrador, el auditor y los roles de consulta lo traen) y
  **`Platform.DeadLetters.Reprocess`** para reprocesar (sólo el administrador). No hay un rol «Supervisor» sembrado: si su cooperativa
  tiene un rol de supervisor de caja, el administrador le da `View` (y `Reprocess` si quiere) en **Administración › Roles**.

## 5. Impresión directa (por caja)

En **Ventas › Puntos de venta**, al editar una caja, junto a «Copias», está **«Imprimir directo a la impresora por defecto»**:

- Con la marca, al cobrar la tirilla **no se muestra en pantalla**: salen las copias de la caja y la pantalla queda lista para la venta
  siguiente. Si la impresión falla, la tirilla queda en pantalla para reimprimirla (Ctrl+Alt+R).
- **En el navegador**, el diálogo de impresión del propio navegador sólo desaparece si el equipo de la caja abre Chrome en modo quiosco de
  impresión: crear un acceso directo con `chrome.exe --kiosk-printing https://app.ingenia365.com/pos` (la impresora predeterminada de
  Windows debe ser la térmica).
- **En la app instalada** la impresión sigue pasando por la hoja de compartir del sistema; la impresión silenciosa nativa necesita integrar la
  impresora térmica y está pendiente.
