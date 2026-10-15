# Cartera: gobierno, seguridad y mensajes al deudor

> Feature 013 (cartera financiera), fase 4 (US17, entrega K1). Complementa
> [contracts/api.md](../../specs/013-cartera-financiera/contracts/api.md) §1, §2, §3.5–§3.10, §5 y §14,
> [data-model.md](../../specs/013-cartera-financiera/data-model.md) §3 y §26 y
> [contracts/adaptadores-y-archivos.md](../../specs/013-cartera-financiera/contracts/adaptadores-y-archivos.md) §4.
> Aquellos son el contrato; esto es la receta para quien parametriza y opera Cartera y para quien
> escribe la historia siguiente sobre estas piezas.

Antes del primer crédito, Cartera necesita su gobierno: quién ve qué (alcance), quién decide hasta
cuánto (atribuciones), qué órganos deciden lo que nadie decide solo (actas y votos), qué no puede hacer
la misma persona dos veces sobre el mismo expediente (segregación), quiénes son vinculados, y cómo se
le escribe al deudor sin romper la Ley 2300. Todo esto opera en K1 por HTTP y por pantalla.

## 1. Alcance: falla cerrado

Un usuario de Cartera ve y toca **sólo** las oficinas y las líneas que tiene asignadas. Sin filas, no ve
nada: el alcance falla cerrado (`IAlcanceDeCartera`, `AlcanceDeCartera.Vacio`). Lo que está fuera de su
alcance responde el mismo 404 que lo que no existe, y la auditoría lo anota como
`Lending.Access.OutOfScope`.

| Dimensión | De dónde sale | Cómo se asigna |
|---|---|---|
| Oficinas | `SEC_UserBranchAssignments` → `ADM_Branches` → `COR_Branches.TenantBranchPublicId` (`OficinasDelUsuario`) | Seguridad > Usuarios (`POST /api/admin/users/{id}/branches`) |
| Líneas | `LND_UserCreditLineScopes` | `/cartera/alcances?usuario=` (`PUT /api/lending/scopes/users/{userPublicId}`, `Lending.Scopes.Manage`) |
| Todas las oficinas / todas las líneas | los permisos `Lending.Scope.AllOffices` y `Lending.Scope.AllLines` | en el rol |

**Una oficina sin agencia contable no cuenta.** El salto oficina → agencia lo hace
`COR_Branches.TenantBranchPublicId`, que se elige en Maestros > Agencias (columna «Oficina»; una oficina,
una sola agencia: `Branch.OfficeAlreadyLinked`). Una agencia sin oficina vinculada deja fuera de alcance
todo lo que se registre en ella para quien no tenga todas las oficinas.

Los procesos de fondo (tareas programadas, despachador) ven todo; el maestro también.

## 2. Atribuciones

### 2.1 La matriz

`/cartera/atribuciones` (`Lending.Attributions.View`; publicar `Lending.Attributions.Manage`). La matriz
se publica **entera** en cada versión —niveles y reglas— con su fecha de vigencia y su motivo; las
anteriores quedan para leer cómo se decidió algo en su fecha (`VigenciaBitemporal`).

- **Niveles**: individuales (con un permiso `Lending.Decisions.Analyst|Coordinator|Management|Exceptions` y
  un techo de monto consolidado) y colegiados (con un órgano que decide crédito). Debe haber exactamente un
  nivel colegiado final sin techo (`Lending.Attribution.MatrixWithoutTopLevel`), a lo sumo un nivel de
  excepción y un nivel de consejo (`Lending.Attribution.MatrixInvalid` con `data.reason`).
- **Reglas**: suben de nivel por condición (línea, modalidad, plazo, garantía, mora, concepto, excepción a la
  política). El nivel efectivo es el mayor por rango entre el techo, las reglas, el consejo forzado por un
  vinculado (FR-040) y la excepción por puntaje o alertas.
- **No exceptuables** (`NoExceptuables`): lo que ningún nivel puede saltarse; la vista previa los lista.

La **vista previa** «¿a qué nivel iría?» (`POST /api/lending/attribution-matrices/preview`, sin
`Idempotency-Key`) recibe el monto como consolidado; US2 le suma la exposición del deudor.

### 2.2 Montos operativos

Cuatro atribuciones operativas admiten un monto máximo por rol en `SEC_PermissionAmountLimits`
(`/cartera/atribuciones`, pestaña «Montos operativos»; `POST /api/lending/amount-limits`):
`Lending.Disbursements.Execute`, `Lending.CreditLimits.Assign`, `Lending.ValidationFailures.Confirm` y
`Lending.Payments.BackdateBeforeClose`. Cualquier otro código responde `NotLimitable` con la lista de los
cuatro. Sin fila, el rol no tiene tope.

## 3. Órganos, sesiones, actas y votos

### 3.1 Órganos

Pestaña «Órganos» de `/cartera/actas` (`Lending.GoverningBodies.View`; administrar
`Lending.GoverningBodies.Manage`). Cada órgano tiene código de la cooperativa (hasta 10, `CodigoDeCatalogo`),
clase (consejo, junta de vigilancia, comité de crédito, comité de riesgos, gerencia, otro), si decide
crédito y el prefijo de sus actas.

- **Un solo consejo activo** (`Lending.GoverningBody.BoardAlreadyActive`).
- **Reglas con vigencia**: quórum sobre la composición, mayoría ordinaria (sobre los presentes elegibles o la
  composición) y, en el consejo, la **fracción calificada de vinculados**. Una regla no rige desde antes de
  hoy; la anterior rige hasta la víspera.
- **Fracción de vinculados**: en una cooperativa con actividad financiera (perfil regulatorio vigente; sin
  perfil se asume que sí) no puede ser menor que `VINCULADOS_FRACCION_CALIFICADA` (4/5, Ley 454 art. 61):
  `Lending.Parameter.BelowLegalFloor` con `data { limitCode, limitValue, source }`.
- **Miembros con vigencia**: personas del maestro, con papel y voto; un suplente se nombra atado a un
  principal vigente del mismo órgano y se sienta por él cuando falta. Terminar una vigencia pide motivo.
  La lista sólo muestra nombre y papel (C84).

### 3.2 Sesiones

`/cartera/actas` (`Lending.Sessions.Read`; convocar, asistencia y puntos `Lending.Sessions.Manage`; cerrar
`Lending.Sessions.Close`). La lectura de un acta y de su PDF queda auditada como `Lending.Session.Viewed`.

1. **Convocar**: órgano, clase (ordinaria, extraordinaria), modalidad y fecha. Nace en `Draft`.
2. **Asistencia**: los miembros vigentes a la fecha, presentes o no, con suplencias; los invitados van por
   su persona y no cuentan. La primera asistencia abre la sesión (`Open`) y **sella** la regla vigente en la
   fecha, la composición y el quórum.
3. **Puntos**: cada punto es un asunto de un sujeto (`LendingDecisionSubjects`) que alguna historia registró
   con su `IAsuntoDeActa`. En K1 sólo existe `ActivationDifferences` (la carga de migración aplicada); US2
   suma la solicitud y el cupo, y así las demás. Un punto `Decision` decide; un punto `Recommendation`
   recomienda a otro órgano. El sistema calcula la regla (`RelatedPartyQualified` si un solicitante es
   vinculado y la cooperativa tiene actividad financiera) y los excluidos (radicador, solicitantes, la
   persona por la que existe el vínculo).
4. **Votos**: tres maneras, según quién registra:

   | Método | Quién | Permiso |
   |---|---|---|
   | `RecordedBySecretary` | la secretaría, el voto de cualquier miembro | `Lending.Sessions.RecordVotes` |
   | `OwnSession` | el miembro, desde su sesión, sólo el propio | `Lending.Sessions.Vote` |
   | `InPersonPasskey` / `InPersonTotp` | el miembro presente en el equipo de la sala, con su llave o su código | cualquiera de los dos, más el desafío de presencia (`POST /{id}/presence-challenge`) |

   Sin el permiso que corresponde: 422 `Lending.Permission.Required` con `data.permissionCodes`. Un no
   miembro, un excluido o un ausente: `Lending.Session.VoterExcluded` con `data.reason`. Sin quórum no se
   vota. **Un voto mal registrado no se edita**: se registra otro con `replacementReason`; el anterior queda
   con `ReplacedAt` y cuenta el vigente.
5. **Cerrar**: con la huella del acta que se revisó (`currentContentSha256` del `GET`; si algo cambió,
   `Lending.Decision.ContentChanged`). Sin quórum, `Lending.Session.QuorumNotMet` con `{ required, present }`.
   El cierre es todo o nada: evalúa cada punto con `EvaluadorDeVotacion`, escribe las decisiones, aplica cada
   asunto, numera el acta con el prefijo del órgano, sella la huella y guarda el PDF como adjunto de módulo no
   borrable. Quien cierra **debe tener persona** (segregación) y lo cerrado no se edita.

El **PDF** (`GET /api/lending/sessions/{id}/minutes.pdf`) de una sesión abierta es el borrador marcado
«SIN CERRAR» (`acta-borrador-….pdf`); fuera de producción lleva «AMBIENTE DE PRUEBAS – SIN VALIDEZ».

### 3.3 El acta como respaldo de otros actos

Todo acto «con acta» (aceptar diferencias, activar Cartera, castigo, evaluación de cartera, política SARC)
recibe el `sessionItemPublicId` de un punto `Decision` **aprobado** de una sesión **cerrada** con el sujeto
esperado, y lo valida `PuntoDeActaCerrado` (`Lending.Session.ItemMismatch` con
`{ expectedSubject, itemSubject, itemOutcome }`). La activación lo usa desde la fase 4.

## 4. Segregación

`ReglasDeSegregacion` compara **personas**, no usuarios (dos usuarios de la misma persona son la misma
persona). Fijo, no se apaga: quien radica no decide ni vota lo suyo. Configurable por vigencia en
`Segregacion.Incompatibilidades` (permiso propio `Lending.Segregation.Manage`): por defecto `Decidir ×
Desembolsar` activo, y los sugeridos inactivos (`Analizar × Decidir`, `Desembolsar × AnularOrdenDeGiro`,
`ArmarLoteDeCastigo × AplicarCastigo`, `CargarMigracion × AceptarDiferencias`). Un choque responde
`Lending.Segregation.Conflict` con la acción previa y su instante; cada acto deja su intervención en
`LND_CaseParticipations` en el mismo guardado (`RegistroDeIntervenciones`).

**Todo usuario que radique, decida, vote, cierre un acta o desembolse necesita su persona del maestro**
(`SEC_Users.PersonId`); sin ella, `Lending.Security.UserWithoutPerson` con `data.howToFix`. Se asocia con
`PUT /api/admin/users/{publicId}` y `personPublicId` (la pantalla de usuarios todavía no lo ofrece: ver §8).

## 5. Vinculados

`/cartera/vinculados` (`Lending.RelatedParties.Read`, lectura auditada `Lending.RelatedParty.Viewed`;
registrar y terminar `Lending.RelatedParties.Manage`). Fundamentos: consejero, miembro de la junta de
vigilancia, representante legal, aportante significativo (con su porcentaje), cónyuge y pariente (estos dos
**con la persona del vínculo**, `Lending.RelatedParty.LinkRequired`; el pariente con clase y grado, que no
pasa de `VINCULADOS_GRADO_PARENTESCO`). Vigencias del mismo fundamento no se cruzan
(`Lending.RelatedParty.Duplicate`). Carga masiva con la plantilla `lending.related-parties`
(`/api/lending/related-parties/template.xlsx` e `import?mode=review|apply`): no crea personas, y lo
registrado prevalece sobre lo migrado. Un solicitante vinculado lleva el asunto al consejo con la mayoría
calificada.

## 6. Autorizaciones, auditoría, parámetros, plantillas y alertas

- **Autorizaciones** (`/cartera/autorizaciones`, `Compliance.HabeasData.ViewHistory`; registrar
  `Compliance.HabeasData.RecordConsent`): por finalidad, con su evidencia, versión de la política y origen.
  Una vigente **sin evidencia** (lo migrado) bloquea centrales y cobranza (FR-006,
  `Lending.Authorization.WithoutEvidence`), no la entrega de un documento.
- **Auditoría**: todo el grupo `/api/lending` audita el intento sin permiso (`Lending.Access.Denied`, antes
  del 404) y el rechazo de forma (`Lending.Request.Rejected`, con el comando, los campos y los códigos de la
  regla, **nunca los valores**); los comandos llevan oficina y dispositivo (`X-Dispositivo`). Los eventos
  `Lending.*` se sellan en la cadena (`COR_AuditOutbox`, retención de diez años) y se verifican en
  `/admin/auditoria`, pestaña «Integridad» (`AuditLog.VerifyIntegrity`): una alteración o un borrado en Mongo
  sale como `Altered` o `Deleted`.
- **Parámetros** (`/cartera/parametros`): claves agrupadas, valor vigente y su fuente, límites legales y
  «pendiente de validar». Rige lo más estricto entre la cooperativa y el límite nacional; una vigencia menos
  estricta responde `Lending.Parameter.BelowLegalFloor`.
- **Plantillas de carga** (`/cartera/plantillas`) y **alertas** (`/cartera/alertas`): las alertas de Cartera
  se ven con el permiso destinatario **y** el alcance de la oficina y la línea de la alerta.

## 7. Mensajes al deudor

### 7.1 Quién recibe y quién envía

Sólo el **deudor** o un **codeudor obligado** del crédito (`Lending.DebtorMessage.RecipientNotObligated`);
nunca una referencia. La entrega de un documento va a quien lo pidió y no necesita crédito. Todo mensaje
nace en la cola (`ColaDeMensajesAlDeudor`, el único escritor de `LND_DebtorMessages`) en la misma
transacción que lo motiva, con su plantilla, versión, canal, asunto y cuerpo **sellados** al encolar; es
inmutable y su historia sólo crece (`LND_DebtorMessageEvents`). Antes de activar Cartera no se encola nada.

`/cartera/mensajes` (`Lending.DebtorMessages.Read`; enviar `.Send`; cancelar `.Cancel`).

### 7.2 La ventana de la Ley 2300

`VentanaLegalDeContacto` decide al encolar y **otra vez al despachar**. Horarios
(`Cobranza.HorarioLunesAViernes`, `Cobranza.HorarioSabado`) y tope diario (`Cobranza.ContactosPorDia`) de
la cooperativa, acotados por los límites nacionales `COBRANZA_*` (7:00–19:00 lunes a viernes, 8:00–15:00
sábados, un contacto por día; semilla pendiente de validar). Domingos y festivos nacionales no; un solo
canal de cobranza por semana. Motivos (`ContactWindowReason`):

| Motivo | Qué significa |
|---|---|
| `RecipientNotObligated` | no es deudor ni codeudor obligado |
| `DebtorDeceased`, `Insolvency`, `CollectionSuspended` | no se cobra (sí el informativo en insolvencia) |
| `ChannelNotAuthorized`, `AuthorizationWithoutEvidence` | falta la autorización del canal o su evidencia |
| `NoDestination` | el maestro no tiene correo, celular o dirección para ese canal |
| `Sunday`, `Holiday`, `OutsideHours` | fuera del horario legal |
| `DailyLimitReached`, `WeeklyChannelLocked` | tope del día o canal de la semana ya usado |
| `NoWindow` | no hay ventana en los 14 días siguientes |
| `ChannelUnavailable` | el canal no respondió |

La **entrega de un documento** pedida por el titular está exenta de horarios y topes (regla 7), pero
necesita la autorización de tratamiento general.

### 7.3 Enviar, programar y cancelar

- **Inmediato** (sin `scheduledFor`): si la ley no lo permite ahora se rechaza
  (`Lending.DebtorMessage.OutsideLegalWindow` con `data.nextAllowedAt`, `.WeeklyChannelUsed`,
  `.ChannelNotAuthorized`). La pantalla muestra antes la ventana (`GET /contact-window`).
- **Programado**: nace `Queued` o `Deferred` con el instante permitido siguiente; `cartera.mensajes` (cada
  minuto, 200 por pasada) lo despacha cuando llega su hora.
- **Cancelar**: sólo en `Queued` o `Deferred`, con motivo; después, `Lending.DebtorMessage.AlreadySent`.
- **Reintentos**: un canal no disponible difiere; tras `Mensajes.Reintentos` queda `Failed` con la alerta
  `Cartera.MensajeAlDeudorFallido`. `cartera.entregas` consulta la entrega de lo enviado en las últimas 72 h.

### 7.4 Plantillas y variables

`/cartera/plantillas-de-mensaje` (`Lending.MessageTemplates.View`; versionar `.Manage`). Nueve plantillas
sembradas «pendiente de validar» (`AVISOPREV`, `RECORDPREV`, `MORAADMIN`, `PREJURID`, `NOAPLICPAG`,
`PASOACAJA`, `ABONOCAP`, `GASTOCOBR`, `ENVIODOC`), una por canal. Se cambian **creando una versión**: la
anterior rige hasta la víspera y lo ya encolado conserva la suya. Cada propósito tiene su lista blanca de
variables (las comunes —`NombreDelTitular`, `NombreDeLaCooperativa`, `TelefonoDeLaCooperativa`,
`NumeroDelCredito`, `FechaDeCorte`— más las suyas); otra variable responde
`Lending.MessageTemplate.VariableNotAllowed` con `data.allowed[]`. Un SMS largo avisa sus segmentos
(`SmsTooLong`); WhatsApp no sale sin el identificador de la plantilla aprobada por el proveedor.

### 7.5 El canal simulado

Sin SMS ni WhatsApp configurados en `COR_ExternalChannelSettings`, el canal es `SIMULADO`: en DEV y QA
nada sale por SMS ni WhatsApp. El mensaje queda `Simulated` con el destino copiado del maestro al
despachar. El simulado responde por el último dígito del documento del titular (o del destino): 1 dato
inválido, 3 fallo de entrega, 5 canal no disponible; los demás, enviado y entregado. En producción el
simulado no llama a nada. El correo sale por el de la plataforma (`CORREO`).

## 8. Preparación para la activación

Lo que tiene que estar antes de activar Cartera (F4, F5; quickstart §2.7):

1. **Roles**: `POST /api/lending/activation/suggested-roles` crea en bloque los trece roles de las
   plantillas `cartera.*` (`CARTERA-ADMINISTRADOR`, `CARTERA-ASESOR`…; repetir no duplica). Son roles
   normales de la cooperativa: se ajustan sin tocar la plantilla, en **Seguridad › Roles** (`/security/roles/{id}`,
   la matriz de permisos del rol; `PUT /api/admin/roles/{id}` con la lista de permisos; quien lo hace necesita
   `Security.Roles.View` y `Security.Roles.Update`, y no se puede quitar el último rol con usuarios activos que puede
   volver a editar roles, `LastAdminLockout`). Por ejemplo, el
   asesor (`cartera.asesor`) trae `Lending.CreditLines.View` —**sólo consultar** las líneas, respuesta del dueño del
   2026-10-14—: la cooperativa que no quiera que su asesor las vea se lo quita a `CARTERA-ASESOR`, y la que quiera
   que otro rol las consulte se lo agrega; nada de eso toca la plantilla ni a las demás cooperativas.
   `cartera.administrador` no decide ni vota
   (C86); `cartera.comite-credito` y `cartera.consejo` son la **secretaría** de su órgano (registran votos y
   cierran): a los miembros que votan desde su sesión se les da `Lending.Sessions.Vote` en un rol aparte.
2. **Personas de los usuarios**: cada usuario que radique, decida, vote, cierre o desembolse, asociado a su
   persona del maestro (`PUT /api/admin/users/{id}` con `personPublicId`). Pendiente: la pantalla de usuarios
   no tiene todavía el selector de persona.
3. **Oficinas vinculadas**: cada oficina con su agencia contable (Maestros > Agencias) y cada usuario con
   sus oficinas; las líneas, cuando existan (US1), en `/cartera/alcances`.
4. **Órganos y matriz**: el consejo y los comités con sus reglas y miembros, y la matriz de atribuciones.
5. **El acta**: una sesión del órgano con el punto `Decision` de sujeto `ActivationDifferences` sobre la
   carga aplicada, votado y cerrado; su `sessionItemPublicId` va en `POST /api/lending/activation`. Las e2e
   lo hacen así por HTTP (`CarteraE2E.ActivarCarteraAsync`).

## 9. Cómo se prueba

- Casos dorados de votación, atribuciones y ventana legal en `Domain.Tests/Lending`; segregación en
  `ReglasDeSegregacionTests`.
- Application: `SessionCommandsTests`, `GoverningBodyCommandsTests`, `AttributionMatrixCommandsTests`,
  `RelatedPartyCommandsTests`, `ColaDeMensajesAlDeudorTests`, `TareasDeMensajesAlDeudorTests`,
  `DebtorMessageCommandsTests`, `AuditoriaDeCarteraTests`, `AlcanceDeCarteraTests`.
- e2e (colección «Cartera e2e», `DB_PROVIDER=PostgreSql|SqlServer`): `SeguridadYHabeasDataDeCarteraTests` y
  `MensajesAlDeudorTests` sobre los usuarios de `UsuariosDeCartera`; `ActivacionDeCarteraDePruebaTests` con
  el acta por HTTP. Lo que necesita créditos lo suman las fases siguientes a esas mismas clases.
