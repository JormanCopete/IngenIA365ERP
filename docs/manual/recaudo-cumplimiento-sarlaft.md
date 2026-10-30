# Recaudo: cumplimiento y SARLAFT básico (para el oficial de cumplimiento)

> Feature 015, entrega R0, US14 (FR-094 a FR-098, FR-189, FR-191, FR-202 a FR-204 parte R0). Receta para **el oficial de
> cumplimiento** (plantilla `cumplimiento.oficial`). Todo umbral es un **parámetro con vigencia** por cooperativa; los valores
> sembrados son **pendientes de validar** (P7).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Registro de transacciones en efectivo por persona y mes (lectura auditada) | `/recaudo/cumplimiento` | `Receipts.Compliance.View` |
| Señales evaluadas | `/recaudo/cumplimiento` (pestaña «Señales») | `Receipts.Compliance.View` |
| Operaciones retenidas: liberar o devolver por orden de pago | `/recaudo/cumplimiento` (pestaña «Retenidas») | `Receipts.Compliance.Release` |
| Parámetros `Sarlaft.*` | parámetros de Recaudo (`RCD`) | según la pantalla de parámetros |

## 2. Los parámetros

| Clave | Defecto | Uso |
|---|---|---|
| `Sarlaft.UmbralDeclaracionOrigenFondos` | 5.000.000 | Efectivo por operación desde el que la caja exige la declaración de origen de fondos y quien efectúa |
| `Sarlaft.UmbralTransaccionIndividual` | 5.000.000 | Transacción individual del reporte a la UIAF (CE 72 de 2024 prorrogada por CE 95 de 2026) |
| `Sarlaft.UmbralTransaccionesMultiples` | 30.000.000 | Múltiples del mes calendario por titular (alerta `Recaudo.AcumuladoDeEfectivo`; misma fuente) |
| `Sarlaft.Fraccionamiento` | `3;7` | Operaciones bajo el umbral de declaración que en esa ventana lo alcanzan juntas (alerta `Recaudo.Fraccionamiento`) |
| `Sarlaft.PagadorTerceroFrecuente.Titulares` | 3 | Titulares distintos para los que una persona entregó efectivo en el mes |
| `Sarlaft.ConsignanteRepetido.Partidas` | 2 | Partidas por identificar abiertas del mismo consignante |
| `Sarlaft.ListasNoVinculantes.Efecto` | `Sigue` | Una coincidencia no vinculante sigue con alerta o retiene el dinero |
| `Sarlaft.Reporte.Estado` | `Vigente` | Si el reporte del período se genera o está suspendido |
| `Sarlaft.Reporte.Alcance` | `EfectivoChequeYPlataforma` | Medios que entran: efectivo, cheque y plataforma virtual (Anexo 2); también `EfectivoYCheque` o `SoloEfectivo` |
| `Sarlaft.Reporte.ExigeAusencia` | sí | Sin transacciones, recordar el reporte de ausencia en SIREL (no va en el archivo) |
| `Sarlaft.Reporte.CodigoDeOrganizacion` | (vacío) | Código SSTTTCCCCC de la cooperativa ante la UIAF; sin él no hay archivo |
| `Sarlaft.Reporte.UsuarioSirel` | (vacío) | Usuario SIREL con que se carga el reporte; sin él no hay archivo |
| `Sarlaft.Reporte.TiposDeProducto` | `Loan=08;Account=01;MiscConcept=17;…` | Código de producto (tabla 7.1) por clase de aplicación; `Account:AP=70` lo precisa por el prefijo del número |

Lo que la norma impone **no tiene parámetro para dejar de bloquear**: una lista vinculante siempre bloquea.

## 3. Declaración y quien efectúa

Sobre el umbral, la caja pide la declaración una vez por recibo, con su soporte, y la viaja a cada consignación que la exija; guarda
la versión del parámetro que la exigió. Pide también el documento y nombre de quien entrega el dinero.

## 4. Listas restrictivas en la caja

Pagador, titular, quien efectúa y beneficiarios se consultan en el contexto «recaudo». Coincidencia **exacta** = vinculante: la
operación queda bloqueada y el oficial recibe la alerta inmediata `Recaudo.ListaVinculante`. Coincidencia **posible** = no vinculante:
según el efecto, sigue con alerta o el cajero **retiene** el efectivo en custodia de su sesión. Usted la **libera** (otra persona que
quien retuvo, con soporte y la sesión abierta) o la **devuelve por orden de pago**; si Tesorería no la gira, la custodia se reabre.
La regla «exacta vinculante, posible no vinculante» es una decisión del dueño **pendiente de su validación (P7)**.

## 5. Registro y señales

Cada recibo con efectivo deja una fila en el registro (pagador y quien efectúa); su anulación, otra con signo contrario. El acumulado
del mes sólo cuenta lo de Recaudo en R0. Las señales **sólo informan** (ninguna bloquea): acumulado, fraccionamiento, pagador tercero
frecuente y consignante repetido; cada una queda en el registro de señales con la versión del parámetro y se alerta una vez por
persona, señal y mes.

## 6. El reporte de transacciones a la UIAF (Anexo 2 TRSO)

Desde la segunda ronda del 2026-10-27 el archivo es el del **Anexo 2 del Título V de la Circular Básica Jurídica** (documento técnico
TRSO, actualizado en diciembre de 2022), cotejado posición por posición con el PDF:

- **Formato**: texto de ancho fijo, 645 posiciones por registro, en ANSI CP1252 (las tildes y la Ñ en mayúscula se conservan), con un
  encabezado (código de la organización, fecha de corte y total), **una línea por transacción** (22 campos) y una cola. Nombre:
  `TRSOSSTTTCCCCCMMAA.txt` —la sigla, el código de la organización, el mes y el año del período—.
- **Qué entra**: primero las **múltiples** (las transacciones del mes de un mismo titular que juntas llegan al umbral), y de ésas se
  reporta **cada operación por separado**; después las **individuales** que llegan al umbral solas. Ninguna aparece dos veces. Por
  defecto cuentan el efectivo (recibos, egresos de caja y punto de venta), los cheques recibidos en ventanilla y lo recibido por
  plataforma virtual (canal en línea); la transferencia que se registra en ventanilla no es ninguno de los tres.
- **Cada línea** lleva la fecha, el valor en pesos sin decimales, la moneda (COP), la oficina (su código, o su número interno si no
  tiene), el **tipo de producto** del parámetro, si es **retiro (1) o depósito (2)**, el **medio** (0 efectivo, 1 cheque, 2 plataforma
  virtual), el número del producto sin separadores, el **titular** y **quien realiza** la transacción (quien entregó o recibió el
  dinero en la ventanilla) con el tipo de identificación de la tabla 7.4 (13 cédula, 22 cédula de extranjería, 31 NIT, 41 pasaporte,
  12 tarjeta de identidad…), su dígito de verificación (el del NIT, o -1) y nombres y apellidos o razón social (-1 en lo que no
  aplica), y el municipio DANE de la oficina. El producto es el de la aplicación de mayor valor del recibo.
- **Antes de generar** registre el código de la organización y el usuario SIREL, y el municipio de cada oficina: sin ellos el reporte
  queda «validado» con las inconsistencias y sin archivo.
- **La ausencia** de transacciones no es un archivo: se presenta en **SIREL** dentro de los 20 días calendario siguientes al corte. La
  generación del mes sin transacciones lo recuerda y admite **Radicado de ausencia (SIREL)** para dejar constancia. El plazo es
  informativo: nada se bloquea por él.

## 7. Pendiente de validar (P7)

Los umbrales vigentes del sector solidario, el estado de la suspensión del reporte, el alcance, el reporte de ausencia, las señales de
fraccionamiento y las listas vinculantes que la plataforma consulta. **Bloquea la salida** en umbrales y listas: se registran como
versiones de los parámetros `Sarlaft.*` antes del go/no-go.
