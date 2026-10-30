# Salida en vivo de Recaudo y Tesorería en COOFLOPAL (del 23/11 al 04/12/2026)

> Feature 015, US24 (FR-225 a FR-228). Cronograma y pasos de la salida de R0/T0. Lo que es del dueño está marcado; lo de producción
> sólo con respaldo por base y su autorización expresa. Runbooks vecinos: [despliegue](recaudo-tesoreria-despliegue.md),
> [paralelo en QA](recaudo-tesoreria-paralelo-en-qa.md), [go/no-go](recaudo-tesoreria-go-no-go.md).

## 1. Cronograma

| Fecha | Qué | Quién |
|---|---|---|
| 11/11–27/11 | Paralelo en QA y su informe `parallel-comparison` | equipo de COOFLOPAL |
| 23/11–25/11 | **Ensayo general** en QA: recorridos A–L de quickstart §3, cada uno con el dinero, los módulos y la contabilidad cuadrados | dueño y equipo |
| 26/11 | Correcciones del ensayo (un cambio de esquema es un par nuevo y aditivo) | desarrollo |
| 27/11 | Respaldo por base y despliegue a producción (runbook de despliegue) | dueño autoriza |
| 28/11 | **Go/no-go** en producción y **carga definitiva** de las plantillas de puesta en marcha sobre la base limpia | dueño |
| 01/12 | Activar Recaudo y Tesorería (con cuadre provisional si no hay definitivo); después, Cartera y Aportes; abrir la ventanilla | dueño |
| 02/12–04/12 | **Cuadre definitivo** de apertura contra Contabilidad | dueño y contadora |

## 2. Antes del 28/11

1. Manuales de R0/T0 entregados y capacitación hecha: [cajero](../manual/recaudo-manual-del-cajero.md),
   [supervisor](../manual/recaudo-manual-del-supervisor.md), [analista](../manual/recaudo-manual-del-analista.md),
   [tesorero](../manual/tesoreria-manual-del-tesorero.md), [contabilización](../manual/recaudo-y-tesoreria-contabilizacion.md),
   [cumplimiento](../manual/recaudo-cumplimiento-sarlaft.md).
2. Pendientes del dueño que bloquean la salida: matrices contables (P12), umbrales y listas SARLAFT (P7), escenarios de no-go parcial (P15).
3. La puerta T0301 en verde (e2e de Cartera y Captaciones sobre los módulos reales en PostgreSQL y SQL Server) y, después de ella, la
   migración de datos `RetiroDeLosDoblesDePrueba` y el borrado del código de los dobles. **Hasta la puerta el código de los dobles sigue
   en el repositorio**; en producción nunca hay dobles registrados.

## 3. Carga definitiva (28/11)

En `/recaudo/puesta-en-marcha` y `/tesoreria/puesta-en-marcha`, con la fecha de corte del 30/11 (el día anterior a la activación):
puntos y cajas, cajeros y alcances, medios por punto, bóvedas al corte, partidas al corte, cheques en canje; cuentas bancarias con su
saldo, chequeras, cheques en tránsito, remesas y consignaciones en tránsito; cuentas bancarias de personas (las aprueba otra persona);
y las cifras de referencia. Cada plantilla es todo o nada, con revisión previa y error por fila y columna; ninguna genera hechos
contables. Antes, comprobar que producción no tiene ningún dato del paralelo.

## 4. Activación (01/12)

1. **Cuadre**: en la puesta en marcha de cada módulo, si el cuadre contra Contabilidad no da cero, registrar el **provisional** con
   motivo y **fecha límite** del definitivo (entre el 02/12 y el 04/12); lo aprueba quien tenga `…Activation.ProvisionalSquare` (P18:
   quién es, **pendiente del dueño**).
2. **Activar Recaudo** (`/recaudo/activacion`) y **Tesorería** (`/tesoreria/activacion`): en el mismo guardado quedan registrados
   `REC` (recaudador de obligaciones y de cuentas, destino `Receipts`) y `TES` (destino `Treasury`).
3. Comprobar en `/admin/participantes-de-integracion` el registro real y, en la activación de Cartera y de Aportes, el recaudador y la
   tesorería en verde; **activar Cartera y Aportes**.
4. Recalcular la lista de go/no-go y abrir la ventanilla.

## 5. Mientras el cuadre siga provisional

- El **cierre contable de diciembre se niega** (`Accounting.Period.OpeningSquarePending`).
- Cada día llega la alerta `Recaudo.CuadreProvisionalPendiente` / `Tesoreria.CuadreProvisionalPendiente`; vencida la fecha límite,
  **escala** también al dueño.
- Al registrar el **definitivo** (diferencia cero, o con acta aceptada), queda enlazado al provisional, las alertas se cierran y el
  cierre del mes ya no se niega. Anotar aquí la fecha y quién lo registró (T0344).

## 6. Escenarios de no-go parcial (P15, pendiente del dueño)

Qué hace la ventanilla si Cartera o Aportes no se activan el 01/12 y su plan de reversa: se escriben, se aprueban y se adjuntan al ítem
manual `NOGO-PARCIAL` de la lista de go/no-go. **Bloquea la salida**: sin el escenario no se decide una salida parcial. La decisión
tomada se anota en esta sección.
