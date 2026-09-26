# Auditoría con entrega garantizada y cadena de sellos

> Feature 012, fase 2 (plataforma) y entrega I1 (T36–T38, FR-007, FR-008, SC-012). Cómo llega a Mongo la
> auditoría de los módulos del comercio sin perderse, cómo se sella para que un cambio se note, cómo se
> verifica, qué hacer si la verificación encuentra algo y cómo se rota la clave de las anclas. Diseño en
> [decisiones-transversales.md](../../specs/012-inventario-comercial/decisiones-transversales.md) T36–T38 y
> [data-model.md](../../specs/012-inventario-comercial/data-model.md) §23.

## 1. Por qué

Hasta la 012 cada evento de auditoría iba **directo a Mongo** desde el proceso: si el proceso caía entre
el `SaveChanges` y la escritura en Mongo, el cambio quedaba en SQL y su auditoría no existía. Y un
documento de Mongo se podía reescribir sin que nada lo delatara. Para el inventario, la facturación y la
caja —donde la DIAN, la revisoría y el fondo de garantías piden rastro de diez años— ninguna de las dos
cosas es aceptable.

## 2. Qué módulos van por aquí

Los **módulos encadenados** (`AuditoriaEncadenada.Modulos`): `Inventory`, `ElectronicInvoicing`,
`Integration`, `Approvals`, `Alerts`, `Parameters`, `Taxes`, `PaymentMeans` y `Navigation` (el ingreso a
cada opción del ERP). **`Accounting` queda fuera** en esta feature (pregunta C1), y Nómina, Personas,
Identidad y el resto siguen por el camino de siempre (`MongoAuditService`, directo). Todos los encadenados
se conservan **diez años** (`AuditRetention.ModulosDeDiezAnios`).

## 3. Las tres tablas (en la base de cada cooperativa)

| Tabla | Qué es | Quién la escribe |
|---|---|---|
| `COR_AuditOutbox` | cada evento de un módulo encadenado, escrito **en la misma transacción** que el cambio; después del reenvío queda la fila **delgada** (`Seq`, `EventId`, `Hash`, `PrevHash`, `Forwarded`, `ForwardedAt`) con `PayloadJson = NULL`. **Nunca DELETE** (Principio VII) | lo agregan `AuditableEntityInterceptor` (las diferencias de entidad), `AuditBehavior` (el evento del comando), un contexto aparte para los **rechazos** (sobrevive al rollback) y `AuditoriaEncadenada.RegistrarAsync` (ingresar, exportar, imprimir); sólo el reenviador la actualiza |
| `COR_AuditChainHeads` | la cabeza de cada cadena: `Stream`, `LastSeq`, `LastHash`, con `RowVersion` | el reenviador, en la misma transacción que sella |
| `COR_AuditAnchors` | anclas: el hash de un punto de la cadena firmado con HMAC (`Kind` = `Genesis`, `EveryN` cada 1.000 eventos, `Daily`); `KeyVersion` de la clave. Hecho inmutable | el reenviador |

Hay **una cadena por cooperativa** y clase de retención: el flujo `{tenantPublicId:N}:10y`. Los eventos van
a la base Mongo de la cooperativa (`IngenIA365ERP_Audit_{tenantPublicId:N}`, colección `audit_events`) con
`_id = EventId` y un bloque `chain { stream, seq, prevHash, hash, alg, v }`.

## 4. El reenviador (`AuditOutboxForwarder`)

Servicio de fondo registrado **sólo** en `API/Program.cs` y condicionado a
`Integration:AuditForwarder:Enabled` (el DbMigrator nunca lo arranca). En cada pasada recorre las
cooperativas activas y, en cada una, por `IEjecutorEnCooperativa` con el actor «Proceso de integración»
(origen `Tarea:audit.forward`), toma el arrendamiento `audit.forward` y deja el trabajo a
`SelladoDeAuditoria`, en tandas y en este orden:

1. **Sellar.** Las filas sin `Seq`, en orden de `Id` (nunca desde «el último Id»: una transacción que
   confirma tarde entra en la pasada siguiente), reciben `Seq`, `PrevHash` y
   `Hash = SHA-256(PrevHash ‖ JSON canónico del documento)` (`SelloDeIntegridad`), y la cabeza avanza, todo
   en un `SaveChanges`. Si otra réplica selló a la vez, la cabeza choca por `RowVersion`, se descarta y se
   relee: **la cadena no se bifurca**. El primer sellado de un flujo crea la cabeza y el ancla génesis.
2. **Reenviar.** Las selladas y no reenviadas, **en orden de `Seq`**, se insertan en Mongo (una clave
   duplicada es que ya estaba) y se marcan `Forwarded` con `PayloadJson = NULL`. Si una falla, suma
   `ForwardAttempts`, deja `LastForwardError` y **se detiene ahí**: no salta una para mandar la siguiente.
3. **Anclar.** Cada 1.000 eventos al sellar y una vez al día sobre la cabeza, si avanzó.

Sellar antes de reenviar es a propósito: al revés, Mongo podría tener un evento con una posición que la
cadena no le dio. La exactitud no depende del arrendamiento: la sostienen el `RowVersion` de la cabeza y
el `_id` de Mongo.

**Una carga ilegible** (un `PayloadJson` que no se puede leer) no se sella —no se podría verificar—, suma
intentos, deja `[Auditoria.CargaIlegible]` en el log y **no detiene a las demás**. Queda sin `Seq` y se
reintenta en cada pasada.

## 5. El sello y las anclas

`SelloDeIntegridad` (`Infrastructure/IngenIA365ERP.Audit/Integrity`) es **la única canonicalización**, para
sellar y para verificar: claves en orden ordinal y sin espacios, instantes UTC con milisegundos y `Z`,
decimales invariantes con su escala, y la versión `v` en la raíz. Entra todo el documento de Mongo salvo
`chain.hash`; cambiar cualquier byte cambia el hash. Cambiar el formato exige otra versión `v`, nunca
reescribir ésta. Casos dorados en `Application.Tests/Infrastructure/Audit/Casos`.

El ancla es `HMAC-SHA256(stream|seq|hash|anchoredAt)` con la **clave de anclas** de la sección
`AuditSignature`: la versión `AnchorKeyVersion`, que es **otra** que la de los PDF de exportación
(`CurrentKeyVersion`) y **nunca** `dev-v1`. Quien reescribiera la cadena entera en Mongo y en SQL no podría
rehacer las anclas sin esa clave. Si la clave de anclas no está configurada, no está en `Keys` o es
`dev-v1`, **no se ancla** (la cadena se sigue sellando) y el log dice `[Auditoria.AnclaSinClave]`.

## 6. La verificación

`POST /api/audit/integrity/verify` con `{ from, to, stream? }` (permiso `AuditLog.VerifyIntegrity`; es
consulta, sin clave de idempotencia), o la pestaña **«Integridad»** de la consola de auditoría
(`/admin/auditoria`, `AuditLogConsole`). Sin `stream`, el de diez años de la cooperativa; el rango no pasa
de diez años. `VerifyAuditIntegrityQuery`:

- toma como **testigo** las filas delgadas de `COR_AuditOutbox` en SQL (que nadie borra) y, para cada
  posición del rango, lee el documento de Mongo y **recalcula** su hash;
- valida cada ancla del rango con la clave de **su** versión;
- responde `{ stream, fromSeq, toSeq, checked, anchorsChecked, incidents[] }` y deja el evento
  `AuditLog.IntegrityVerified` con el resultado (`Clean` o las clases encontradas).

| Hallazgo | Qué significa |
|---|---|
| `Altered` | el contenido, el hash guardado o el enlace con el anterior no cuadran: alguien cambió el documento |
| `Deleted` | falta una posición (salto de secuencia) o el documento que la ocupaba |
| `Interleaved` | otro documento ocupa la posición |
| `PurgedByRetention` | falta porque venció su plazo de diez años: esperado, no es incidente |
| `AnchorInvalid` | un ancla no firma lo que dice o no coincide con la cadena |

**Lo que el plan prometía y no existe todavía**: la verificación **nocturna** de la cadena de auditoría.
La tarea nocturna que corre `ProgramadorDeTareas` en I1 es la del **kardex** (`inventario.integridad`,
02:00), no la de la auditoría; la cadena se verifica **a mano** (pantalla o ruta). Tampoco levanta una
alerta: un hallazgo sólo queda en la respuesta y en el evento. Una tarea `ITareaProgramada` que la corra
cada noche por cooperativa y levante una alerta con `IAlertas` es el siguiente paso; mientras tanto, la
revisión periódica es una tarea de quien tenga `AuditLog.VerifyIntegrity` (el auditor).

## 7. Ante un hallazgo

1. **No corregir nada en Mongo ni en SQL.** El hallazgo es evidencia; tocarlo la destruye.
2. Repetir la verificación sobre un rango más chico alrededor del `seq` para ver si es uno o varios.
3. Si es `PurgedByRetention`, no hay nada que hacer.
4. Si es `Altered`, `Deleted` o `Interleaved`: exportar el rango (la consola exporta firmado), guardar la
   respuesta de la verificación, comparar la fila delgada de SQL (`EventId`, `Hash`) con el documento de
   Mongo y revisar quién tiene escritura sobre la base de auditoría de esa cooperativa. El usuario de Mongo
   de la API **sólo debería poder insertar** en `IngenIA365ERP_Audit_{cooperativa}` (T986, del dueño): si
   puede actualizar o borrar, esa es la primera causa a descartar.
5. Si es `AnchorInvalid`: confirmar que la versión de clave del ancla (`KeyVersion`) sigue en
   `AuditSignature:Keys` con su mismo secreto. Una clave borrada o cambiada da este hallazgo en **todas**
   las anclas de esa versión (§8); una sola ancla inválida entre válidas es alteración.
6. Avisar al dueño y al revisor fiscal con la evidencia. Es un incidente de seguridad, no un defecto de
   programa.

**Si la cadena no avanza** (sin hallazgos, pero la consola no muestra lo reciente de un módulo encadenado):

```sql
SELECT COUNT(*) FILTER (WHERE "Seq" IS NULL) AS sin_sellar,
       COUNT(*) FILTER (WHERE NOT "Forwarded" AND "Seq" IS NOT NULL) AS sin_reenviar,
       MAX("ForwardAttempts") AS max_intentos
FROM dbo."COR_AuditOutbox" WHERE NOT "Forwarded";
SELECT "EventId", "Seq", "ForwardAttempts", "LastForwardError" FROM dbo."COR_AuditOutbox"
WHERE NOT "Forwarded" AND "LastForwardError" IS NOT NULL ORDER BY "Id" LIMIT 20;
SELECT "Stream", "LastSeq", "UpdatedAt" FROM dbo."COR_AuditChainHeads";
```

Y en el log de la API: `[Auditoria.ReenvioFallido]` (Mongo caído o sin permiso: se reintenta solo en la
pasada siguiente), `[Auditoria.CargaIlegible]` (una fila que no se sellará nunca: es un defecto a corregir
en el programa que la escribió), `[Auditoria.SelladoConcurrente]` (normal con dos réplicas),
`[Auditoria.AnclaSinClave]` (falta la clave de anclas). Comprobar también que
`Integration:AuditForwarder:Enabled` no esté en `false` en ese ambiente.

## 8. La clave de las anclas y su rotación

La sección `AuditSignature` de la API tiene `CurrentKeyVersion` (la de los PDF exportados),
`AnchorKeyVersion` (la de las anclas) y `Keys` (versión y secreto en base64). **Los valores por defecto
del código son de desarrollo**: `dev-v1` para los PDF y `dev-anclas-v1` para las anclas, **con su secreto
en el repositorio**. En un ambiente compartido hay que configurar las dos con secretos propios, por el
Secret del clúster, nunca en un JSON del repositorio. Antes de desplegar I1 en producción hay que
comprobar que la de producción no es la de desarrollo; si lo es, se rota (T986, del dueño).

Rotar la clave de anclas:

1. Generar un secreto de 32 bytes (`openssl rand -base64 32`) y una versión nueva (`pdn-anclas-AAAAMMDD`).
2. **Agregarla** a `AuditSignature:Keys` en el Secret del ambiente, **sin quitar las anteriores**.
3. Cambiar `AuditSignature:AnchorKeyVersion` a la nueva y reiniciar la API.
4. Las anclas nuevas se firman con ella; las viejas se siguen verificando con la suya, porque cada ancla
   guarda su `KeyVersion`.
5. **Nunca borrar una versión** de `Keys` mientras haya anclas firmadas con ella (diez años): la
   verificación las daría todas `AnchorInvalid`.

Si se sospecha que una clave de anclas se filtró, se rota igual y se anota desde qué fecha rige la nueva:
las anclas anteriores a esa fecha prueban menos.

## 9. Lo que queda abierto

- **La verificación nocturna** de la cadena y su alerta (§6).
- **Copia externa de las anclas** (pregunta C2): hoy el ancla vive en SQL con HMAC. Copiar cada ancla a
  un almacén inmutable (S3 con Object Lock, como los respaldos) cerraría el caso de quien tenga a la vez
  la base, Mongo **y** la clave. Decidido como «después».
- **Contabilidad fuera** de la cadena (pregunta C1).

## 10. Dónde está cada cosa

| | |
|---|---|
| Módulos encadenados y flujo | `Application/Common/Audit/AuditoriaEncadenada.cs` |
| Entidades | `Domain/Entities/Audit/{AuditOutboxEntry,AuditChainHead,AuditAnchor}.cs`, `Domain/Enums/Audit/AuditAnchorKind.cs` |
| Escritura en la transacción | `Persistence/Interceptors/AuditableEntityInterceptor.cs`, `Application/Common/Behaviors/AuditBehavior.cs` |
| Reenviador y sellado | `Infrastructure/IngenIA365ERP.Audit/Services/{AuditOutboxForwarder,SelladoDeAuditoria}.cs` |
| Sello | `Infrastructure/IngenIA365ERP.Audit/Integrity/SelloDeIntegridad.cs` |
| Claves | `Infrastructure/IngenIA365ERP.Audit/Configuration/AuditSignatureSettings.cs`, `Services/AuditSignatureService.cs` |
| Verificación | `Application/Audit/VerifyIntegrity/VerifyAuditIntegrityQuery.cs`; ruta en `API/Endpoints/AuditLogModule.cs`; pestaña en `Shared/Pages/Administracion/AuditLogConsole.razor` |
| Migración | `PlataformaParaInventario` (aditiva) |
