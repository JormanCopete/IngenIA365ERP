# Cartera: líneas de crédito y usura

> Feature 013 (cartera financiera), fase 5 (US1, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §3.1–§3.3,
> [contracts/plantillas.md](../../specs/013-cartera-financiera/contracts/plantillas.md) §1 y
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §1.2–§1.9.
> Aquellos son el contrato; esto es la receta para quien parametriza las líneas y para quien escribe la
> historia siguiente sobre ellas (simulación, originación, desembolso, ventas de Comercio).

Una línea de crédito es la regla con la que nace un crédito: modalidad, tasas, plazos, plan, seguros,
garantías, documentos, cupo. El sistema **nunca** deja registrar una línea cuya tasa efectiva, con los
cargos que computan, supere la usura certificada de su modalidad.

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Lista con estado, modalidad, versión vigente, tasa, holgura de usura y la marca «cerrada por usura desde …» | `/cartera/lineas` | `Lending.CreditLines.View` |
| Alta (línea y primera versión), carga masiva con la plantilla | `/cartera/lineas` | `Lending.CreditLines.Manage` |
| Detalle, historial, versión nueva, cerrar, reabrir, borrar | `/cartera/lineas/{id}` | `.View` / `.Manage` |
| Puntaje de la línea | pestaña «Puntaje» del detalle | `Lending.Scoring.View` / `Lending.Scoring.Manage` |
| Equivalencias de Comercio | pestaña de `/cartera/lineas` | `.View` / `.Manage` |

Las líneas son también una dimensión del **alcance**: un usuario sólo ve las líneas que tiene asignadas
(`/cartera/alcances`, ahora con un selector de líneas por código y nombre) o todas con
`Lending.Scope.AllLines`. Fuera de su alcance, una línea responde el mismo 404 que una que no existe y la
auditoría anota `Lending.Access.OutOfScope`. La plantilla de rol `cartera.asesor` no trae
`Lending.CreditLines.View`: si el asesor debe consultar las líneas en pantalla, se le agrega en un rol
aparte.

## 2. Versiones: por qué los créditos vivos no cambian

Nada de una línea se edita en su sitio. Cada cambio es una **versión nueva** desde una fecha —nunca antes
de hoy (`Lending.CreditLine.RetroactiveVersion`) ni el mismo día ni antes que la última registrada
(`Lending.CreditLine.VersionOverlaps`)— con su motivo. La versión anterior rige hasta la víspera; el
historial muestra cada versión con quién y cuándo la registró, y una huella (`ContentSha256`) de todo su
contenido, hijas incluidas.

El crédito sella la versión con la que nació: tasa pactada, convención de días, plan, prelación. Una
versión nueva sólo rige para lo que se origine desde su fecha; los créditos vivos conservan lo pactado
(FR-032). Por eso «Nueva versión» parte de la vigente: copia todo y se cambia sólo lo que cambia.

El código de la línea es la nomenclatura de la cooperativa (`CodigoDeCatalogo`, 10 caracteres, en
mayúsculas) y **no cambia**: es la dimensión con que Contabilidad reparte los hechos de Cartera.

Una línea nace en **Borrador** y pasa a **Activa** cuando su versión vigente pasa la usura (y, si admite
ventas de Comercio con interés, tiene concepto tributario). **Cerrar** (con motivo) la saca de los créditos
nuevos —no se simula, ni se radica, ni se vende sobre ella— sin tocar los vivos; **reabrir** revalida la
usura del día. **Borrar** es lógico y sólo procede sin solicitudes, créditos, cupos ni mensajes; si los
hay, la respuesta es `Lending.CreditLine.HasLoans` con la alternativa de cerrarla.

## 3. Cómo se controla la usura

La usura no se compara con la tasa nominal ni con la EA digitada a secas: se compara con la **tasa
efectiva con cargos**, la TIR diaria de los flujos del crédito con sus fechas reales, anualizada sobre 365.

- **Con qué usura.** La de la modalidad certificada que el `CatalogoDeModalidades` vigente asigna a la
  combinación modalidad + sub-segmento (consumo, comercial y ordinario comparten
  `CONSUMO_Y_ORDINARIO` en el catálogo propuesto; microcrédito, `MICROCREDITO`). La registra el
  administrador de la plataforma como valor nacional `USURA` y la réplica la copia a cada cooperativa
  (ver [valores-nacionales.md](../operaciones/valores-nacionales.md)).
- **Cuándo.** A la fecha desde la que rige la versión **y** a hoy: una versión futura tiene que caber en
  las dos.
- **Sobre qué.** Sobre un **plan tipo** por banda: el monto máximo de la banda (o de la línea), sus plazos
  mínimo y máximo y cada periodicidad admitida. Si **una** banda falla, no se guarda nada.
- **Qué cargos computan.** Los seguros que la cooperativa **retiene** en parte (`porcentajeRetenido`) o que
  autoasegura; un seguro que se paga entero a la aseguradora no computa. La convención de días también
  pesa: con real/360 una EA bajo la usura puede quedar por encima en efectiva.
- **Tasa variable** (D10): el margen se suma en la expresión del índice (IBR nominal más puntos), se pasa a
  EA y se acota por el piso y el techo; el control usa el índice publicado del día.
- **Mora.** Una mora fija nunca supera la usura del día; la relativa (factor sobre la corriente) se topa
  al causar.

Si la usura se supera, la respuesta es 422 `Lending.Usury.Exceeded` con la tasa efectiva, la usura usada,
su clave de modalidad, la fecha, el valor nacional que la certificó y el campo que la supera; la pantalla
lo muestra en español («la tasa efectiva con cargos 27,1 % EA supera la usura de 25,62 % de
CONSUMO_Y_ORDINARIO por 1,48 puntos»). Si falta la usura de esa fecha, la respuesta es
`NationalValues.Missing` y queda la alerta `Cartera.ValorNacionalFaltante` aunque el registro falle.

**El panel de usura** del editor consulta `usury-check` al salir de cada campo de tasa: muestra la usura
certificada a la fecha de la versión, la efectiva con cargos de la versión vigente y su holgura, y cuánto
queda la tasa digitada por debajo o por encima de la usura. El control exacto de la versión digitada (con
sus bandas, sus seguros y su convención) lo hace el sistema al registrarla. `usury-check` no escribe nada.

## 4. Bandas, seguros, garantías y documentos

- **Bandas** (hoja `Tasas`): por monto, plazo en meses, tipo de deudor (asociado o cliente), empleados y
  antigüedad mínima; en una línea fija cada banda lleva su tasa, en una variable su margen. Dos bandas de
  las mismas dimensiones no se cruzan (`Lending.CreditLine.RateBandsOverlap`).
- **Seguros**: la aseguradora es una persona del maestro; base saldo o monto inicial, tarifa por período,
  periodicidad, obligatorio, vida deudores, y si la cooperativa retiene parte de la prima (entonces
  computa para la usura).
- **Garantías**: por rango de monto y plazo, codeudores mínimos, tipos admitidos, cobertura mínima y si
  los aportes cuentan.
- **Documentos**: código, nombre, obligatorio, por monto, tipo de deudor y forma de pago. Sin los
  obligatorios, la solicitud no pasa a análisis (US2).

## 5. Concepto tributario del interés (D20)

Una línea que admite ventas a crédito de Comercio con una tasa mayor que cero **no se registra** sin el
concepto tributario del interés —gravado o no gravado— y su soporte
(`Lending.CreditLine.InterestTaxConceptMissing`). El soporte es el concepto de la contadora, subido como
adjunto de la línea desde su detalle (dueño `CreditLineTaxSupport`: se sube con
`Lending.CreditLines.Manage`, se lee con `.View` y no se borra, porque respalda versiones selladas). Como
el adjunto cuelga de la línea, una línea de Comercio se registra primero sin ventas de Comercio y la
versión que las habilita, con su concepto y su soporte, se agrega desde el detalle. En la plantilla, la
columna `soporteDelConceptoTributario` lleva el identificador del adjunto.

## 6. Equivalencias de Comercio

Las ventas a crédito llegan con la periodicidad y la unidad de plazo **como texto** (`Monthly`,
`Biweekly`, `Days`…). Las equivalencias las traducen a Cartera: una periodicidad, o los días por unidad
de plazo. Son de la cooperativa, no de una línea, y se versionan desde una fecha con motivo. Una venta
cuya periodicidad no tiene equivalencia queda rechazada y **reprocesable**: se registra la equivalencia y
se reprocesa el mensaje.

## 7. La plantilla `lending.credit-lines`

Seis hojas, encabezados en la fila 1 (la mecánica común está en
[plantillas-de-importacion.md](plantillas-de-importacion.md)):

| Hoja | Una fila es | Llave |
|---|---|---|
| `Lineas` | una versión de una línea | `codigo` + `vigenteDesde` |
| `Tasas` | una banda de esa versión | `linea` + `vigenteDesde` + dimensiones |
| `Documentos` | un documento exigido | `linea` + `vigenteDesde` + `codigo` |
| `Seguros` | un seguro (la aseguradora por su documento) | `linea` + `vigenteDesde` + `codigo` |
| `Garantias` | una regla de garantía | `linea` + `vigenteDesde` + rangos |
| `Equivalencias` | una equivalencia de Comercio | texto + `vigenteDesde` |

- **Las mismas reglas que en pantalla**, con el mismo código de error en la fila y la columna: usura de la
  versión y de cada banda, modalidad y sub-segmento del catálogo vigente, concepto tributario, código
  repetido, valor nacional faltante. `VIVIENDA` llega con K6b (`Import.Cell.NotYetAvailable`).
- **Porcentajes en puntos** con hasta cuatro decimales (22,5 = 22,5 % EA); se guardan como fracción.
- **Revisar** no guarda y lista `errors[] { sheet, row, column, code, message }`; **aplicar** es todo o
  nada (`Import.Invalid` con la revisión en `data`). Revisar y aplicar son dos operaciones con su propia
  clave.
- La misma línea con un `vigenteDesde` posterior crea una versión y **reemplaza enteras** sus hijas: lo que
  la versión nueva no trae, no lo tiene. Una versión que empieza antes de hoy en una línea con solicitudes
  o créditos se rechaza. Crear o cerrar versiones pide motivo; `activa = no` cierra la línea.
- `factorDeMora` (múltiplo de la corriente) está **pendiente de validar por la contadora**.

## 8. Cuando llega una usura nueva

La réplica de valores nacionales corre, en la misma transacción, el efecto
`EfectoDeReplicacionUsuraEnLineas` cada vez que trae una `USURA` o un catálogo de modalidades nuevo, haya
o no Cartera activada:

1. Cada línea no cerrada cuya modalidad usa esa usura se controla con la versión que rige el día de la
   usura nueva (con cada banda, sobre el plan tipo).
2. Si alguna la supera, la línea queda **cerrada por usura desde** ese día (`UsuryBlockedSince`): sigue
   activa, pero no admite créditos nuevos, y se alerta `Cartera.LineaCerradaPorUsura` (una por línea; no
   se repite si ya estaba bloqueada).
3. Los créditos vivos no se tocan: su causación diaria se topa sola con la usura de cada día y alerta
   `Cartera.UsuraTopada` (US4).
4. El bloqueo se limpia registrando una **versión nueva bajo la usura** desde ese día.
5. Si el catálogo nuevo retira la modalidad de una línea, la línea se **cierra** con motivo
   «Modalidad retirada del catálogo nacional» y se alerta.

Un día sin usura registrada no se adivina: registrar una versión desde ese día responde
`NationalValues.Missing` y alerta `Cartera.ValorNacionalFaltante`. Esa alerta se guarda **fuera** de la
operación que falla (`AlertasDiferidas`, que `IdempotencyBehavior` vacía al terminar): hasta el
2026-10-10 se guardaba dentro de la transacción del comando y se revertía con él.

## 9. Cómo se prueba

- Motor puro: casos dorados de plan y usura en `tests/IngenIA365ERP.Domain.Tests/Lending/Casos/`
  (Python `decimal` de 40 dígitos, `calcular.py`) y la propiedad de la suma del capital.
- Application: alta, versión, ciclo de vida, `usury-check`, puntaje, equivalencias, plantilla y el efecto
  de la réplica (`tests/IngenIA365ERP.Application.Tests/Lending/Configuration/`, `.../Imports/`).
- Cliente: `LineasDeCreditoClientTests` (claves de idempotencia, sin `Authorization` a mano).
- e2e en los dos motores: `LineasDeCreditoYUsuraTests` y el alcance por línea de
  `SeguridadYHabeasDataDeCarteraTests` («Cartera e2e», con la usura de consumo y microcrédito y un IBR
  registrados en la central del host) y `UsuraNuevaCierraLineasTests` («Valores nacionales e2e», porque
  registra usuras que cambian en la central).
