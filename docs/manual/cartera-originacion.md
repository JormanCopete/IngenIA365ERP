# Cartera: originación, decisiones, cupos y bloqueos

> Feature 013 (cartera financiera), fase 6 (US2, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §1.3–§1.5, §1.9, §1.13, §4.1–§4.5,
> §5, §7.1, §7.2 y §7.4, y el recorrido de
> [quickstart.md](../../specs/013-cartera-financiera/quickstart.md) §3.5. Aquellos son el contrato; esto es la
> receta para quien implanta y para quien opera la originación: el asesor que simula y radica, el analista y
> la gerencia que deciden, el comité y el consejo que deciden en acta, el jefe de cartera que otorga cupos y
> el oficial de cumplimiento que libera las coincidencias en listas.

La originación es el camino de una persona desde «¿cuánto me queda la cuota?» hasta un crédito aprobado (o un
cupo otorgado). El sistema hace solo las validaciones, lleva la solicitud al nivel que le toca según la
matriz de atribuciones y deja cada decisión como un hecho con huella. Hay cuatro cosas que **ningún nivel
puede saltarse**: la falta de capacidad de pago, la falta de autorización del titular, una coincidencia en
listas restrictivas y la usura.

Prerrequisitos: líneas de crédito activas ([cartera-lineas-de-credito.md](cartera-lineas-de-credito.md)),
matriz de atribuciones, órganos, alcance por oficina y línea, y la persona del maestro asociada a cada usuario
que radique, decida o vote ([cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md)).

## 1. Pantallas y permisos

| Qué | Dónde | Permiso |
|---|---|---|
| Simular, imprimir y enviar la simulación | `/cartera/simulador` | `Lending.Simulations.Run` |
| Lista de solicitudes | `/cartera/solicitudes` | `Lending.Applications.Read` |
| Nueva solicitud y su borrador (partes, documentos, autorizaciones, preferencia) | `/cartera/solicitudes/nueva` | `Lending.Applications.Create` |
| Radicar (enviar a análisis) | detalle `/cartera/solicitudes/{id}` | `Lending.Applications.Submit` |
| Registrar el análisis y volver a correr validaciones | detalle | `Lending.Applications.Analyze` |
| Anular o desistir | detalle | `Lending.Applications.Cancel` |
| Decidir (individual) | detalle o `/cartera/aprobaciones` («Mis pendientes») | `Lending.Applications.Read` **más** el permiso del nivel: `Lending.Decisions.Analyst`, `.Coordinator`, `.Management` o `.Exceptions` |
| Sesiones y actas (nivel colegiado) | `/cartera/actas` | `Lending.Sessions.Read` · `.Manage` · `.RecordVotes` · `.Vote` · `.Close` |
| Pedir consulta a centrales · ver el informe | detalle de la solicitud o del cupo | `Lending.CreditBureau.Query` · `Lending.CreditBureau.ReadReports` |
| Cupos, disponible y saldos de aportes | `/cartera/cupos` | `Lending.CreditLimits.Read` |
| Otorgar, terminar y revalidar cupos; importar saldos de aportes | `/cartera/cupos` | `Lending.CreditLimits.Assign` (atribución con monto) |
| Preferencia de abono de un cupo rotativo | `/cartera/cupos` | `Lending.PrepaymentPreferences.Manage` |
| Ver bloqueos · registrar y levantar bloqueos y embargos | `/cartera/cupos` | `Lending.Loans.Read` · `Lending.Blocks.Manage` |
| Coincidencias en listas pendientes de liberar | sección de `/cartera/cupos` | `Compliance.RestrictiveLists.Release` |
| Grupos conectados | pestaña «Grupos conectados» de `/cartera/vinculados` | `Lending.RelatedParties.Read` · `.Manage` |

Sin el permiso de una ruta, o con un recurso fuera del alcance de oficina o línea del usuario, la respuesta es
el **mismo 404** que si no existiera (nunca 403), y la auditoría anota `Lending.Access.Denied` o
`Lending.Access.OutOfScope`. Las listas filtran en silencio lo que el usuario no alcanza.

Toda escritura lleva la cabecera `Idempotency-Key`: un doble clic o un reintento devuelven lo mismo que la
primera vez. Las simulaciones, la vista del enrutamiento y los enlaces de descarga son consultas y van sin
clave.

**Antes de activar Cartera** sólo se simula y se otorgan cupos con su evaluación y decisión. Crear o radicar
una solicitud responde `Lending.NotActivated` («Cartera no activada»).

## 2. Simular

`/cartera/simulador` (`POST /api/lending/simulations/schedule`) calcula el plan cuota a cuota —capital,
interés, seguros, otros y saldo—, la tasa efectiva anual, la nominal del período, la **efectiva con cargos**,
los totales y la explicación por tramo de días. **No registra nada**: ni solicitud, ni número, ni clave de
operación.

- Monto, cuotas, periodicidad, sistema de amortización, gracia o extraordinarias fuera de lo que admite la
  línea responden `Lending.Schedule.OutOfLineRange` con el campo y los límites.
- Una simulación que excede la usura **no es error**: responde con `usury.passes = false` y el aviso
  `Lending.Usury.Exceeded`, para que la persona vea por qué. Radicar, aprobar y desembolsar sí bloquean.
- Sin usura registrada para el día, responde `NationalValues.Missing` y alerta `Cartera.ValorNacionalFaltante`
  a quien la registra. El sistema no adivina la usura.
- **Imprimir** (`/schedule/pdf`): el PDF lleva «Simulación – no constituye aprobación», sin número ni código,
  y fuera de producción la marca «AMBIENTE DE PRUEBAS – SIN VALIDEZ».
- **Enviar** (`/schedule/send`): sólo por correo, como mensaje informativo a la persona simulada, con el PDF
  adjunto. Exige autorización de tratamiento de datos (`Lending.Authorization.Missing`), correo registrado
  (`Lending.DebtorMessage.DestinationMissing`), canal autorizado y la ventana legal de contacto
  (`Lending.DebtorMessage.OutsideLegalWindow` con la próxima hora permitida). Enviar no crea nada del crédito.

La simulación deja una huella (`simulationSha256`) que la solicitud nueva puede traer para partir de ella.

## 3. Radicar

### 3.1 El borrador

`/cartera/solicitudes/nueva` arma la solicitud en `Draft`, sin número. Lleva:

- **Solicitante y codeudores**: personas del maestro, elegidas con el buscador; una persona nueva se crea con
  el diálogo de Personas, nunca desde aquí. Un codeudor repetido responde `Lending.Application.DuplicateParty`;
  un solicitante que no es asociado activo, `Lending.Application.NotAssociate`, salvo que la línea y el perfil
  regulatorio admitan no asociados.
- **Condiciones**: línea, oficina, monto, cuotas, periodicidad, primera cuota, sistema, extraordinarias,
  gracia y seguros. Las mismas reglas de rango y usura del simulador.
- **Capacidad declarada** del solicitante y de cada codeudor (ingreso, egresos y cuotas externas). Sin ingreso
  declarado, la validación de capacidad queda «no disponible» hasta el análisis.
- **Forma de pago**: en K1 sólo nómina propia (`OwnPayroll`), y sólo si la persona es empleada
  (`Lending.Application.PaymentFormNotAllowed`). Un convenio de libranza responde
  `Lending.PayrollAgreement.NotAvailable` hasta K2. La certificación de la nómina va como documento.
- **Autorizaciones por finalidad** (tratamiento para el crédito, consulta a centrales) con su evidencia y la
  versión de la política. Se escriben en la misma transacción de la radicación; las ya vigentes no se repiten.
- **Preferencia del deudor para abonos a capital** (reducir plazo o reducir cuota): obligatoria. Queda
  **sellada en la solicitud** al radicar; su registro formal nace con el crédito al desembolsar (o con el cupo),
  y después se cambia con su propio permiso.
- **Documentos**: se suben como cualquier adjunto (el navegador sube directo al almacenamiento) y se asocian a
  un requisito de la línea. Un requisito que la línea no tiene responde `Lending.Application.RequirementUnknown`.
  Mientras la solicitud no esté decidida se pueden quitar; decidida, `Attachments.OwnerLocked`.

El borrador se edita sólo en `Draft` o devuelto (`Lending.Application.NotEditable`); dos personas editando a la
vez se detectan por versión (`Concurrency.StaleRowVersion`: recargar y repetir).

### 3.2 Radicar

«Radicar» (`POST /applications/{id}/submit`) toma el cerrojo del deudor, corre todas las validaciones para el
solicitante y cada codeudor, calcula el puntaje, enruta al nivel que le toca y numera la solicitud al final
(prefijo `SOL-`). Si falta algo, no se numera nada:

- un documento obligatorio que casa por monto, tipo de solicitante, forma de pago y parte →
  `Lending.Application.DocumentsMissing` con la lista de lo que falta;
- una autorización de tratamiento del solicitante o de un codeudor → `Lending.Authorization.Missing`, que dice
  de quién, para qué y cómo registrarla;
- el usuario sin persona del maestro → `Lending.Security.UserWithoutPerson`.

Quien radica queda registrado como interviniente y, desde ese momento, **no puede decidir ni votar** esa
solicitud.

## 4. Qué valida el sistema

Cada validación queda como una fila con su evidencia y su huella, por clase, parte (solicitante o codeudor) y
etapa (al radicar, antes de decidir y, en la fase siguiente, antes de desembolsar). El detalle de la solicitud
las muestra con su resultado: aprobada, advertencia, bloqueante o no ejecutada.

| Validación | Qué mira | ¿Admite excepción? |
|---|---|---|
| Listas restrictivas | consulta al canal vigente; una coincidencia bloquea y alerta al oficial (§9) | **No** |
| Autorización del titular | tratamiento de datos y, para centrales, consulta con evidencia | **No** |
| Capacidad de pago | ingresos − egresos − cuotas propias y externas − la cuota simulada | **No** |
| Usura | la efectiva con cargos contra la usura del día de la modalidad | **No** |
| Capacidad de descuento | en nómina propia, contra el límite nacional de descuento y la certificación | No (límite legal) |
| Concentración | exposición del deudor **y de su grupo conectado** contra el límite individual sobre el patrimonio técnico vigente | No (límite legal) |
| Bloqueo del deudor | bloqueo o embargo vigente → `Lending.Debtor.Blocked` | No (orden o causa vigente) |
| Centrales | en líneas que la exigen, una consulta vigente y válida (§10) | Según resultado |
| Endeudamiento consolidado | lo que la persona debe como deudor y como codeudor, más reservas y aprobadas sin desembolsar | Sube de nivel |
| Cupo | disponible del cupo aplicable → `Lending.CreditLimit.Insufficient` | Sí, por nivel |
| Requisitos de la línea | antigüedad, edad, vínculo laboral, saldo mínimo de aportes con la fecha de su carga → `Lending.Application.RequirementsNotMet` | Sí, por nivel |
| Codeudores exigidos | según la regla de garantías por monto y plazo → `Lending.Collateral.CoDebtorsInsufficient` | Sí, por nivel |
| Vinculado | si la persona es vinculada, va al consejo (advertencia que sube el nivel) | — |

Un canal caído deja la validación «no disponible», que **bloquea hasta repetirla**: el analista la vuelve a
correr con «Volver a correr validaciones» (`POST /applications/{id}/checks/rerun`, se puede elegir cuáles:
`Screening`, `Bureau`, `Capacity`, `PayrollCapacity`, `Consolidated`, `CreditLimit`, `Concentration`, `Usury`,
`Requirements`). Las advertencias llegan al decisor como alertas.

**Concentración.** El límite individual (`Concentracion.LimiteIndividual`, o `.LimiteIndividualConGarantia`
con garantía admisible) se mide por persona y por grupo conectado. El grupo es la unión de los grupos
registrados vigentes, los vinculados a la misma persona y quienes comparten codeudor en un crédito vivo o una
solicitud aprobada; una persona que llega por dos caminos cuenta una vez. Exceder responde
`Lending.Concentration.LimitExceeded` con el alcance (deudor o grupo), el límite, lo actual, lo pedido, el
exceso, la base y, si es grupo, los miembros que suman. Sin patrimonio técnico vigente (o con uno más viejo que
`Contabilidad.IndicadoresVigenciaMeses`), `Lending.Concentration.TechnicalCapitalUnavailable` y la alerta
`Cartera.PatrimonioTecnicoNoDisponible` al contador.

Los **grupos conectados** se registran con fundamento en la pestaña «Grupos conectados» de `/cartera/vinculados`
(`/api/lending/connected-groups`: registrar, agregar miembro, retirar miembro y terminar, todos con motivo y
`Lending.RelatedParties.Manage`; la lista y el detalle son lectura auditada con `Lending.RelatedParties.Read`).

**Puntaje.** Si la línea tiene modelo de puntaje, la radicación lo calcula y el detalle muestra el aporte de
cada variable. Bajo el mínimo **no rechaza**: lleva al nivel de excepción. Una variable sin dato toma los puntos
de su rango «sin dato», o cero, y la explicación lo dice.

## 5. Cómo se calcula el nivel de decisión

El enrutamiento arma lo que se va a decidir y le pregunta a la matriz de atribuciones vigente. El **nivel
efectivo** es el de mayor rango entre:

1. el **techo de monto**, medido sobre el **monto consolidado** del deudor (lo pedido más lo que ya debe como
   deudor y codeudor), no sólo lo pedido. Una solicitud de 15 millones que cabe en gerencia, con 8 millones de
   codeuda, suma 23 y va al comité;
2. las **reglas** de la matriz (línea, modalidad, plazo, garantía, mora, concepto, excepción a la política);
3. el **consejo**, forzado cuando el solicitante es vinculado, aunque el monto sea pequeño;
4. el **nivel de excepción**, por puntaje bajo o alertas.

El resultado se sella con la versión de la matriz, el nivel, las razones, lo no exceptuable y una huella
(`contentSha256`). `GET /applications/{id}/routing` lo muestra. Si después cambia el consolidado (otra deuda
del deudor, un codeudor nuevo), la huella cambia y la solicitud **se reenruta**.

La vista previa de la matriz (`/cartera/atribuciones`) también suma la exposición del deudor cuando se le da la
persona.

## 6. Decidir

### 6.1 Individual

`/cartera/aprobaciones` («Mis pendientes», `GET /api/lending/pending-decisions`) lista lo que el usuario puede
decidir **ahora** —tiene el permiso del nivel, alcance de oficina y línea, y no está excluido— y, aparte, lo que
está en la agenda de sus órganos. Lo que no puede decidir aparece con el motivo (por ejemplo, porque lo radicó).

Decidir (`POST /applications/{id}/decisions`) admite cuatro resultados:

- **Aprobar**;
- **Aprobar con condiciones** menores: monto o plazo menor, o garantía adicional. Las condiciones se revalidan
  contra la usura y la concentración;
- **Negar**, con motivo obligatorio;
- **Devolver**, con motivo: vuelve a borrador para corregir y conserva el número.

Reglas:

- **Quién decide.** El nivel efectivo lo decide quien tiene su permiso **o el de un nivel individual de mayor
  rango**: una atribución mayor cubre una menor, pero los niveles no se suman. Con sólo un permiso de nivel
  menor, `Lending.Attribution.LevelRequired`; sin ningún permiso de decisión, el 404 de la solicitud. El
  administrador maestro de la plataforma y el administrador de la empresa **no deciden** créditos.
- **Huella vista.** La pantalla manda la huella del enrutamiento que el decisor vio. Si cambió, responde
  `Lending.Decision.ContentChanged` y la solicitud se reenruta: recargar y volver a mirar.
- **Segregación.** Quien radicó no decide (`Lending.Segregation.Conflict`). La regla compara personas, no
  usuarios.
- **Lo no exceptuable.** Con capacidad de pago insuficiente, sin autorización del titular, con coincidencia en
  listas o sobre la usura, ninguna aprobación pasa: `Lending.Attribution.NotExceptionable` con las razones.
  Una excepción a la política en lo que sí la admite exige motivo y queda marcada como excepción.
- **Vigencia.** La aprobación vale los días de `Originacion.DiasVigenciaDeAprobacion` de la línea. Vencida, se
  ve como «aprobación vencida» y el desembolso exige decidir de nuevo; la nueva decisión reemplaza a la vieja,
  que queda en el historial.

Antes de decidir, el sistema vuelve a correr las validaciones en la etapa «antes de decidir» y toma el cerrojo
del deudor, para que dos decisiones simultáneas no consuman el mismo cupo o la misma concentración.

### 6.2 Colegiada, con acta

Un nivel colegiado (comité o consejo) no se decide en la ruta individual: responde
`Lending.Attribution.LevelRequired` con el órgano, y el detalle ofrece «Llevar a sesión». En `/cartera/actas`
se convoca la sesión, se registra la asistencia (quórum sobre la composición vigente), se agrega un punto por
solicitud y se votan. El sistema calcula la regla del punto: mayoría ordinaria del órgano, o mayoría
calificada cuando es un vinculado (en entidad con actividad financiera, cuatro quintos de la composición). El
radicador y el interesado **no votan** (`Lending.Session.VoterExcluded`) y la base sigue siendo la composición.

Al **cerrar el acta** cada punto se aplica a su solicitud o cupo con las mismas revalidaciones que la decisión
individual. Si una falla (por ejemplo, la concentración ya no da), **el cierre entero se revierte** y la sesión
sigue abierta. Cerrada, el acta queda sellada con SHA-256 y su PDF; no se edita: se corrige con otra sesión.
El detalle completo de órganos, votos y presencia con passkey o TOTP está en
[cartera-gobierno-y-seguridad.md](cartera-gobierno-y-seguridad.md) §3.

### 6.3 Anular o desistir

«Anular» o «Desistir» (`POST /applications/{id}/cancel`, con motivo) cierran la solicitud sin generar cartera.
Una solicitud ya desembolsada no se cancela (`Lending.Application.AlreadyDisbursed`).

## 7. Cupos y disponible

Un cupo es **consolidado**, **por línea** o **rotativo** (el que consume Comercial en las ventas a crédito). Se
**calcula** (por ejemplo, saldo de aportes por la relación de la línea) o se **asigna**, siempre con vigencia y
motivo.

**Disponible** = cupo − saldos vivos − reservas de número vigentes − aprobadas sin desembolsar. Se calcula al
consultar y nunca se guarda (`GET /credit-limits/availability`). Con una línea, la respuesta dice cuál cupo
aplica: el de la línea, el consolidado o el rotativo; un rotativo sin línea vale para toda línea de origen
comercial.

**Otorgar** (`POST /credit-limits`, `Lending.CreditLimits.Assign`) evalúa endeudamiento, concentración, listas,
centrales y vinculados, y toma el cerrojo del deudor:

- si el monto cabe en la **atribución operativa** del usuario (su monto máximo en «Montos operativos») y la
  matriz no pide otro nivel, el cupo queda **activo**;
- si no —monto mayor, o un vinculado, o una regla que sube el nivel—, queda **esperando decisión** con su
  enrutamiento, sin error, y se decide como una solicitud: en «Mis pendientes» o en acta
  (`POST /credit-limits/{id}/decisions`). Aprobarlo crea la versión firme; negarlo o devolverlo lo termina;
- con coincidencia en listas, la respuesta es igual un **201**, con el cupo **bloqueado** y el aviso
  `Lending.Screening.Blocked` que trae el resultado a liberar. No es un rechazo porque el resultado tiene que
  quedar guardado para que el oficial lo libere.

Cada cambio es una versión nueva; ninguna se edita. «Terminar» deja el cupo en cero desde el día siguiente a
la fecha dada. «Revalidar» levanta la marca «cupo por revalidar» si ya cumple o la deja con lo que falta. Antes
de activar Cartera, el cupo se evalúa contra la carga migrada vigente como si ya hubiera nacido; al activar,
los que ya no cumplen quedan marcados con la alerta `Cartera.CupoPorRevalidar`, igual que cuando aparece un
vínculo sin decisión del consejo.

La **preferencia de abono** del rotativo se registra al otorgar y se cambia con
`Lending.PrepaymentPreferences.Manage`; la heredan los créditos que nazcan después.

**Saldos de aportes** (provisional mientras Aportes no ofrezca su consulta): se cargan con la plantilla
`lending.contribution-balances` (documento, aportes, ahorro permanente, fecha de corte) desde `/cartera/cupos`;
importar exige `Lending.CreditLimits.Assign`. El cupo y los requisitos usan la última carga con corte hasta la
fecha y sellan cuál usaron: corregir una fila es otra carga y no cambia cupos ya otorgados. Una persona
inexistente o un corte futuro son errores de fila y no se guarda nada.

## 8. Bloqueos y embargos

Un bloqueo vigente impide originar y vender a crédito (no toca el cobro), y lo informa la consulta de cupo de
Comercial. A mano sólo se registran dos clases: **manual** y **embargo**, con motivo y soporte opcional
(`Lending.Blocks.Manage`). Uno igual vigente responde `Lending.Block.Duplicate`.

Los demás son **automáticos** (mora, validación fallida, castigo, coincidencia en listas, retiro del asociado,
fallecimiento, insolvencia, revalidación de cupo) y se levantan solos cuando desaparece su causa: intentar
levantarlos responde `Lending.Block.Automatic` con cómo se levanta. Levantar uno manual exige motivo y se
hace una sola vez.

## 9. Listas restrictivas y su liberación

Cada consulta de Cartera a listas (radicación, cupo, venta y el re-escaneo) queda como un resultado con las
versiones de las listas consultadas. La carga de las listas es de cumplimiento (`/compliance/listas-restrictivas`).

- Una coincidencia o posible coincidencia **bloquea** la solicitud o el cupo, pone el bloqueo automático del
  deudor y alerta `Cartera.CoincidenciaEnListas` al oficial. **Nunca se libera por atribución**: ningún nivel
  de decisión la salta.
- La libera **sólo el oficial** (`Compliance.RestrictiveLists.Release`) desde la sección de pendientes de
  `/cartera/cupos` (`POST /api/lending/screening-results/{id}/release`), con motivo. Quien radicó la solicitud
  o pidió el cupo no puede, aunque tenga el permiso (`Lending.Segregation.Conflict`). Liberar levanta el
  bloqueo y queda auditado; una ya liberada o sin coincidencia responde `Lending.Screening.NotPending`.
- Si la misma persona vuelve a consultarse con **las mismas versiones** de las listas, el resultado nace
  liberado; con una versión nueva, se evalúa otra vez.
- Al aplicarse una carga nueva de listas, la tarea `cartera.re-escaneo-de-listas` revisa a deudores y
  codeudores vivos (sólo con Cartera activada) y bloquea y alerta cada hallazgo.
- Un canal caído o un simulado en producción **no cuentan** como consulta.

## 10. Centrales de riesgo

La consulta (`POST /api/lending/bureau-queries`, `Lending.CreditBureau.Query`) exige la autorización del
titular para consulta a centrales **con evidencia**:

- sin autorización → `Lending.Authorization.Missing`, que explica cómo registrarla;
- con una autorización migrada sin evidencia → `Lending.Authorization.WithoutEvidence`: hay que registrar la
  evidencia en `/cartera/autorizaciones`;
- sin canal configurado → `Core.ExternalChannel.NotConfigured`; canal caído → `Lending.Adapter.Unavailable`.

La respuesta sólo trae el estado (con historia, sin historia, canal no disponible, datos inválidos); el
informe queda como adjunto y **verlo es lectura auditada** con `Lending.CreditBureau.ReadReports`, igual que su
enlace de descarga de 60 segundos. Dentro de `Centrales.DiasDeVigenciaDeConsulta` se reutiliza la última
consulta en vez de pedir otra. En una línea que exige consulta y no la tiene vigente, la validación responde
`Lending.Bureau.QueryRequired`.

**Por qué en producción una consulta simulada no sirve para aprobar.** El canal se sella en cada consulta. El
canal `SIMULADO` existe para ensayar: responde según el último dígito del documento (1 reporte negativo, 2 sin
historia, 3 puntaje bajo, 5 no disponible) y marca el informe como «SIMULADO». En DEV y QA su resultado vale
para recorrer el flujo; **en producción** queda registrado pero nunca es evidencia: la validación de centrales
queda bloqueante y aprobar responde `Lending.Adapter.SimulatedInProduction`. Aprobar con un informe inventado
sería aprobar sin consultar, y la norma exige la consulta real. Antes de salir en vivo, la línea que exige
centrales necesita el canal real configurado (ver `docs/operaciones/canales-externos.md`).

## 11. Errores frecuentes

| Código | Qué significa | Qué hacer |
|---|---|---|
| `Lending.NotActivated` | Cartera no está activada; antes sólo se simula y se otorgan cupos | Terminar la activación o, mientras tanto, trabajar con cupos |
| `Lending.Schedule.OutOfLineRange` | Monto, plazo u otra condición fuera de lo que admite la línea | Ajustar al rango que trae la respuesta |
| `Lending.Usury.Exceeded` | La efectiva con cargos supera la usura del día | Bajar tasa o cargos, o revisar la línea |
| `NationalValues.Missing` | No hay usura registrada para la fecha | Que el administrador de la plataforma la registre |
| `Lending.Application.DocumentsMissing` | Falta un documento obligatorio | Subir los de la lista y radicar otra vez |
| `Lending.Authorization.Missing` / `.WithoutEvidence` | Falta la autorización del titular, o no tiene evidencia | Registrarla con su soporte en `/cartera/autorizaciones` |
| `Lending.Security.UserWithoutPerson` | El usuario no tiene persona del maestro asociada | Asociarla al usuario (ver el manual de gobierno, §4) |
| `Lending.Segregation.Conflict` | La persona ya intervino (radicó o pidió) y no puede decidir, votar o liberar | Que lo haga otra persona |
| `Lending.Attribution.LevelRequired` | El nivel es colegiado, o el usuario tiene un nivel menor | Llevar a sesión o pasarlo a quien tenga el nivel |
| `Lending.Attribution.NotExceptionable` | Hay una causa que ningún nivel puede exceptuar | Corregir la causa (capacidad, autorización, listas, usura); no se aprueba así |
| `Lending.Decision.ContentChanged` | La solicitud cambió desde que el decisor la miró | Recargar, revisar el nuevo enrutamiento y decidir |
| `Lending.Decision.NotPending` | La solicitud ya no espera decisión | Recargar; quizá otro ya decidió |
| `Lending.Adapter.SimulatedInProduction` | La evidencia de centrales es simulada y se está en producción | Consultar por el canal real |
| `Lending.Concentration.LimitExceeded` | La exposición del deudor o su grupo supera el límite individual | Reducir el monto, sumar garantía admisible o no otorgar |
| `Lending.Concentration.TechnicalCapitalUnavailable` | No hay patrimonio técnico vigente | Que el contador registre los indicadores |
| `Lending.Debtor.Blocked` | El deudor tiene un bloqueo o embargo vigente | Revisar el bloqueo en `/cartera/cupos` |
| `Lending.Block.Automatic` | Se intentó levantar a mano un bloqueo automático | Resolver la causa; se levanta solo |
| `Lending.Screening.NotPending` | La coincidencia ya fue liberada o no había coincidencia | Nada que hacer |
| `Lending.CreditLimit.Overlaps` | Ya hay un cupo de esa clase con vigencia cruzada o pendiente | Terminar el anterior o esperar su decisión |
| `Lending.Application.NotEditable` | La solicitud ya no está en borrador | Pedir que la devuelvan si hay que corregir |
| `Lending.AmountLimit.Exceeded` | Una acción operativa con monto supera el tope del rol | Que la haga alguien con atribución mayor |
| `Operation.KeyReused` | Se reusó la clave de una operación con otro contenido | Recargar la pantalla y repetir |

## 12. Cómo se prueba

- Dominio: casos dorados del puntaje en `tests/IngenIA365ERP.Domain.Tests/Lending/Scoring/Casos/` y los casos de
  atribuciones de US2 (tres montos, consolidado sobre gerencia, vinculado, puntaje bajo, no exceptuables) en
  `.../Lending/Attributions/Casos/`.
- Application (`tests/IngenIA365ERP.Application.Tests/Lending/Origination/` y `.../Adapters/`): simulación y
  PDF, borrador y radicación, validaciones, grupo conectado, decisiones y «Mis pendientes», actas, centrales y
  su canal simulado, listas y su liberación, cupos, bloqueos, saldos de aportes y el re-escaneo.
- e2e en los dos motores: `OriginacionConAtribucionesTests` («Cartera e2e»), con cupos en una cooperativa sin
  activar y solicitudes en una activada para la prueba; el recorrido manual está en quickstart §3.5.
