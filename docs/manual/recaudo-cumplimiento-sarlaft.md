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
| `Sarlaft.UmbralTransaccionIndividual` | 5.000.000 | Transacción individual en efectivo |
| `Sarlaft.UmbralTransaccionesMultiples` | 30.000.000 | Acumulado del mes calendario por persona (alerta `Recaudo.AcumuladoDeEfectivo`) |
| `Sarlaft.Fraccionamiento` | `3;7` | Operaciones bajo el umbral de declaración que en esa ventana lo alcanzan juntas (alerta `Recaudo.Fraccionamiento`) |
| `Sarlaft.PagadorTerceroFrecuente.Titulares` | 3 | Titulares distintos para los que una persona entregó efectivo en el mes |
| `Sarlaft.ConsignanteRepetido.Partidas` | 2 | Partidas por identificar abiertas del mismo consignante |
| `Sarlaft.ListasNoVinculantes.Efecto` | `Sigue` | Una coincidencia no vinculante sigue con alerta o retiene el dinero |
| `Sarlaft.Reporte.Estado` · `.Alcance` · `.ExigeAusencia` | `Vigente` · `SoloEfectivo` · no | El reporte de transacciones (su estructura llega en R1, antes del primer vencimiento de enero de 2027) |

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

## 6. Pendiente de validar (P7)

Los umbrales vigentes del sector solidario, el estado de la suspensión del reporte, el alcance, el reporte de ausencia, las señales de
fraccionamiento y las listas vinculantes que la plataforma consulta. **Bloquea la salida** en umbrales y listas: se registran como
versiones de los parámetros `Sarlaft.*` antes del go/no-go.
