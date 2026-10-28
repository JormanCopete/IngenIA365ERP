# Research — 017 Ingreso con correo o usuario de documento

**Fecha**: 2026-10-28 · Spec aprobada (D-01..D-16). Cada decisión: qué se eligió, por qué y qué
se descartó. Las referencias a código son de `origin/develop` `f7237aaa`.

## R1. La identidad global es la cuenta central ampliada, no una tabla nueva (D-01)

**Decisión**: `ADM_CentralUsers` (hoy `IdentityUser<Guid>`) gana tipo y número de documento, usuario
de documento, nombres y apellidos y la fecha de verificación del correo. El celular usa la columna
`PhoneNumber` que Identity ya trae y nadie usa. `UserName` **no se toca**: sigue igual al correo.

**Por qué**: cada usuario de hoy ya es esa fila; ampliarla no crea ni enlaza nada para ellos (regla de
oro). Si el usuario de documento se guardara en `UserName`, habría que reescribir `UserName` y
`NormalizedUserName` de todas las cuentas y cambiaría `FindByNameAsync` para todos.

**Descartado**: tabla `ADM_Identities` aparte (una fila nueva por cuenta y un enlace más que migrar);
reutilizar `UserName` (toca a todos los existentes).

## R2. Regla del usuario derivado y catálogo fijo (D-02, D-12, D-13)

**Decisión**: un único punto, `UsuarioDeDocumento` en Domain (`Domain/Common/Identidad/`), con dos
funciones puras: `Normalizar(tipo, número)` y `Derivar(tipo, númeroNormalizado)`. El catálogo es una
clase estática `TiposDeDocumento` en el mismo sitio:

| Código de acceso | Código en `COR_People.IdType` | Prefijo del usuario | Letras | Longitud | Acceso |
|---|---|---|---|---|---|
| CC | `C` | *(ninguno)* | no | 3–10 | sí |
| CE | `CE` | `CE` | no | 3–10 | sí |
| PPT | `PT` *(nuevo)* | `PPT` | no | 3–15 | sí |
| PEP | `PE` | `PEP` | no | 3–15 | sí |
| TI | `TI` | `TI` | no | 3–11 | sí |
| PA | `PA` | `PA` | sí | 3–16 | sí |
| RC | `RC` | `RC` | no | 3–11 | no |
| NIT | `NI` | `NIT` | no | 5–15 | no |

Longitudes **confirmadas por el dueño el 2026-10-28** (D-17, T001); sólo el pasaporte admite letras. Normalizar: mayúsculas; se
quitan espacios, puntos, guiones y cualquier carácter fuera de `[0-9A-Z]`. Para los tipos sin letras,
un número que tenga letras se rechaza.

**Por qué el usuario no es ambiguo**: el prefijo vacío de la CC sólo admite dígitos y todos los demás
prefijos empiezan por letra; entre los prefijos con letra ninguno es comienzo de otro (`PA`, `PEP` y
`PPT` difieren en la segunda letra). Una prueba (`LosPrefijosDelUsuarioNoSonPrefijoDeOtro`) lo
vigila, y otra (`ElUsuarioSeDerivaEnUnSoloSitio`) prohíbe concatenar tipo y número en otro lugar.

**`PT` en vez de ampliar la columna**: `COR_People.IdType` es `varchar(2)` y lo leen 74 sitios, entre
ellos el mapeo DIAN (`tipos-de-identificacion.json`). Un código interno de dos letras (`PT` → DIAN 48)
no cambia la columna; lo que ve la persona es el prefijo `PPT`. *Descartado*: ampliar a 3 (alter no
destructivo, pero obliga a revisar los 74 lectores).

**RC y NIT** no sirven para entrar, pero sí derivan el usuario de la persona de la cooperativa, porque
la unicidad de `COR_People` pasa a ese campo (D-13) y toda persona necesita uno.

## R3. Unicidad de la persona de cooperativa por usuario (D-13) y quienes leen el documento solo

**Decisión**: `COR_People` gana `Username` (varchar 24), derivado por `UsuarioDeDocumento` en
`PersonFactory` y en `UpdatePerson`. Su índice único `UK_COR_People_Username` **no filtra eliminadas**,
como hoy el de `TaxId`, y reemplaza a `UK_COR_People_TaxId`. `ColisionDeDocumentoAsync` compara por
usuario y conserva `Person.TaxIdDuplicate` y `Person.TaxIdDeleted`.

**Riesgo que el plan absorbe**: hoy el número solo identifica a una persona dentro de la cooperativa,
y hay código que lo usa como llave:

- Diccionarios por `TaxId`, que lanzarían excepción ante dos personas con el mismo número:
  `OpeningCommands`, `PurchaseSuggestionReportQuery`, `ImportSalespeopleCommand`,
  `ImportAssemblyReturnBasesCommand`, `ImportDepositMigrationPartCommand`, `ImportCreditLinesCommand`,
  `ImportRelatedPartiesCommand`, `PayrollAgreementsTemplate`, `ImportMigrationPartCommand`,
  `ContributionBalanceCommands`, `ImportNoveltiesCommand`.
- Búsquedas `TaxId ==`: `GetPersonByDocumentQuery` (`/api/core/people/by-document`),
  `SalesDraftCommands`, `CreditoEnLaVenta`, `BorradorDelPos`, `PrefillSupplierInvoiceQuery`,
  `ConveniosDeLibranza`, `ListLoansQuery`, `ConsumidorFinalSeeder`.

**Regla para todos ellos**: un solo resolvedor, `ResolutorDePersonaPorDocumento` (Application/Common/Personas: lo usan Personas y también Cartera, que no puede depender de Personas),
que recibe tipo opcional y número. Con tipo, busca por usuario. Sin tipo, devuelve la persona si hay una
sola; si hay varias, responde `Person.DocumentAmbiguous` con la lista de tipos y pide el tipo. Las
plantillas de importación ganan una columna de tipo **opcional**: las plantillas viejas siguen sirviendo
mientras no haya ambigüedad. La prueba `NadieBuscaPersonasPorNumeroSolo` prohíbe `TaxId ==` y
`ToDictionary(… TaxId)` fuera del resolvedor.

**Coordinación**: las ramas en curso (015 Recaudo y Tesorería, 016 cargue de archivos) pueden sumar
lectores del documento; al integrar se corre la prueba y se pasan por el resolvedor.

Revisión del 2026-10-28 (T003, sólo lectura). La rama `015-recaudo-tesoreria` en `cb490a64` busca
personas por el número solo en cuatro sitios; los cuatro toman `Take(2)` o `FirstOrDefault`, así que
**no lanzan** ante CC y CE con el mismo número, pero sí pueden elegir a la persona equivocada.
- `Receipts/Agreements/ReferenciasDePago.cs:89`
- `Receipts/BankItems/IdentificacionPorReglas.cs:125`
- `Receipts/CashDesk/CajaCommands.cs:51`
- `tests/.../MigracionDeCaptacionesDePrueba.cs:265` (`Single` en una prueba)

Al integrar la 015 con la 017 hay que pasarlos por el resolvedor; la prueba
`NadieBuscaPersonasPorNumeroSolo` los señala. Las comparaciones contra `DepositorTaxId` o
`BeneficiaryTaxId` no son búsquedas de personas. La rama 016 sólo tiene la spec: no toca código.

**`TaxId` no se reescribe** en las filas existentes: el usuario se deriva del número normalizado sin
tocar el dato de negocio, que viaja a la DIAN y a los informes. Lo que tenga puntos o espacios sale en
el reporte (diagnóstico B5) para que la cooperativa lo corrija. Lo nuevo se normaliza al guardar.

## R4. Correo único en la base y verificación de los existentes (D-03)

**Decisión**:

- `UX_ADM_CentralUsers_NormalizedEmail`, único y filtrado a filas no borradas.
- La migración lleva una **guarda**: falla con un mensaje claro si existen duplicados (diagnóstico A1).
  No corrige nada sola.
- La regla «sólo correo verificado» lee `EmailConfirmed`, que es de Identity. La fecha nueva
  `EmailVerifiedAt` es para auditoría.

**Paso de datos, idempotente**: marca verificado (`EmailConfirmed = true`, `EmailVerifiedAt =` fecha de
migración) toda cuenta no borrada, en estado `Active` y con clave. Es un superconjunto de lo que dijo el
dueño («ya entró o aceptó una invitación»): incluye también la cuenta que tiene clave y nunca entró,
que hoy **sí** podría entrar. Las que quedan fuera (`Pending`, sin clave) no podían entrar antes.

El paso no pasa por `UserManager`. Así no rota `SecurityStamp` ni `ConcurrencyStamp`, y las sesiones
vivas siguen valiendo (SC-002).

## R5. Resolución del identificador al entrar, tiempos y bloqueo

**Decisión**: `ResolutorDeIdentificador` (Application/Identity/Auth) recibe el texto y devuelve la
cuenta o nada:

- Con `@`: busca por correo normalizado **y** exige `EmailConfirmed`.
- Sin `@`: normaliza, quita una `CC` inicial si lo que sigue son sólo dígitos, y busca por usuario
  exacto.

El login lo usa en lugar de `FindByEmailAsync`, y la recuperación de clave también.

- **Tiempo**: si no hay cuenta, se verifica la clave contra un hash BCrypt fijo de costo 11 antes de
  responder, para que las dos ramas tarden lo mismo. Una prueba de integración compara medianas.
- **Bloqueo**: la llave Redis pasa a `intentos:{scope}:u:{centralUserId}` cuando el texto resuelve a
  una cuenta, y a `intentos:{scope}:i:{texto normalizado}` cuando no.
  `ADM_CentralUserLoginAttempts` gana `Identifier` e `IdentifierKind` (correo / usuario / desconocido).
- **Al desplegar**, los contadores viejos por correo dejan de leerse. Un bloqueo en curso se libera
  antes de tiempo, una sola vez: el máximo es una hora. *Descartado*: leer las dos llaves durante una
  transición, porque complica sin proteger nada que importe.
- **Contrato**: el cuerpo del login acepta `identifier` y, por compatibilidad con clientes MAUI viejos,
  sigue aceptando `email`. El validador sólo exige formato de correo cuando el texto lleva `@`.

## R6. Sesiones vivas: nada nuevo en el token

**Decisión**: no se agrega ni se vuelve obligatorio ningún claim, y el `email` del token sigue
igual. La renovación no consulta el documento salvo la guardia de R9, que sólo actúa cuando la
cooperativa ya pasó su fecha.

`PermisosDeLaPeticion`, `CurrentUserService` y la auditoría siguen leyendo lo de hoy. Por eso una
sesión abierta antes del despliegue renueva sin cambios.

## R7. Invitación con documento: identidad pendiente al invitar

**Decisión**: al emitir la invitación, la identidad se busca por usuario derivado.

- **Si no existe**, se crea en estado `Pending`, sin clave, con documento, nombres, celular y correo
  digitado sin verificar. Así quedan reservados el documento y el correo: dos cooperativas que
  invitan el mismo documento al mismo tiempo comparten una identidad (índice único más reintento).
- **Si existe**, la invitación apunta a ella (`ADM_Invitations.CentralUserId`) y sale al correo de la
  identidad: el verificado, o el pendiente si aún no activa. El correo digitado se ignora y queda
  auditado. El mensaje al administrador es el mismo en los dos casos (D-05).
- **Correo pendiente equivocado**: sólo la invitación que creó la identidad puede corregirlo, y sólo
  mientras la identidad no se haya activado nunca.
- **Rol y cargo**: viajan como `RolePublicId` y `PositionPublicId` de la cooperativa (`SEC_Roles`,
  `COR_Positions`). Se validan al emitir, con la cooperativa resuelta, y se aplican al activar.
  La base administrativa guarda sólo los Guid: no conoce catálogos de cooperativa.
- **Cargo (D-15)**: se reutiliza `COR_Positions`, que ya es el catálogo de cargos de cada cooperativa
  (`/api/core/positions`, el mismo que usa la ficha de empleado). Queda en `SEC_Users.PositionId`.
- **Primer administrador (`with-admin`)**: captura los mismos datos. Como no hay roles todavía, entra
  como `CompanyAdmin`, como hoy.

## R8. Activación: tres intentos y «en revisión» (D-06)

**Decisión**:

- La invitación guarda `DocumentAttempts` y copia el máximo de la política de la cooperativa
  (`ADM_TenantIdentityPolicies.MaxActivationAttempts`, por defecto 3).
- Abrir la vista previa con un token válido marca el correo verificado (FR-025). La vista previa nunca
  devuelve el documento.
- Al aceptar, el número digitado se normaliza y se compara con el de la invitación.
- **Al agotar los intentos**: la invitación pasa a `UnderReview`, la membresía también, y se escribe al
  invitador por `IEmailSender` (no hay cooperativa resuelta).
- **El administrador** ve la invitación «en revisión» y la corrige, lo que emite una nueva y deja la
  anterior `Superseded`, o la cancela.
- **Si ya tenía identidad**: la rama «con sesión abierta» compara `CentralUserId` en vez del correo
  del token. La de credenciales existentes confirma con la clave. Ninguna toca clave ni segundo factor.

## R9. Completar el documento: guardia por cooperativa (D-04)

**Decisión**:

- `GET /api/auth/me` informa `documentPending` y, por cada cooperativa activa, la fecha desde la cual
  se exige (`ADM_TenantIdentityPolicies.DocumentRequiredFrom`, nula por defecto). El layout muestra el
  paso posponible.
- `GuardiaDeDocumento.Evaluar` sigue el patrón de `GuardiaDeMetodos` y se consulta en las mismas siete
  puertas que acuñan token con cooperativa.
- **Antes de la fecha** (o sin fecha) siempre deja pasar. **Desde la fecha** responde «te falta
  registrar el documento» con un desafío de propósito `document-required`, que sólo sirve para
  registrarlo. Nunca rechaza sin salida.
- Registrar un documento ya tomado no se guarda y abre un caso `DocumentTaken` para el maestro, sin
  revelar de quién es.

## R10. Corrección, actualización y fusión (D-07, D-08, D-14)

**Decisión**:

- **Quién corrige**: `CorregirDocumentoCommand` lo decide en el servidor. Si la persona tiene una sola
  membresía no revocada y es de la cooperativa del actor, corrige ese administrador
  (`Security.Users.CorrectDocument`, nuevo, sólo `CompanyAdmin`). Si tiene varias, sólo el maestro. El
  administrador recibe `Identity.Document.PlatformOnly`, sin cifras.
- **Motivo y soporte**: motivo obligatorio (`Correction` | `Update`). El soporte es opcional: para el
  administrador, un adjunto de su cooperativa con dueño `CorreccionDeDocumento` (regla en
  `AdjuntosDeModulo`); para el maestro, una referencia de texto. *Descartado*: un almacén de adjuntos
  de plataforma, porque es una pieza nueva sin otro uso.
- **D-08**: no se escribe en ninguna cooperativa. Cada una ve la diferencia en el listado de usuarios
  (`documentMismatch`), calculada al leer entre `SEC_Users.PersonId → COR_People.Username` y el
  usuario central, que se guarda copiado en `SEC_Users.CentralUsername` al activar y al acuñar un token con esa cooperativa si cambió (no en
    cada renovación).
- **Fusión (maestro)**:
  - La rechaza si las dos identidades tienen membresía en la misma cooperativa: primero la retira esa
    cooperativa.
  - **Paso 1**, en la base administrativa y en una transacción: mueve membresías, credenciales del
    segundo factor, invitaciones e historial; inhabilita la de origen, rota su sello de seguridad y deja
    el caso `InProgress`.
  - **Paso 2**, por cooperativa: recorre el directorio y abre cada conexión (Principio IV) para cambiar
    `SEC_Users.CentralUserId`. Es idempotente y reintentable.
  - Cuando todas terminan, el caso queda `Done`.

## R11. Correo y clave (D-09, D-10)

**Decisión**:

- **Cambio de correo**: `ADM_EmailChangeRequests` guarda el código hasheado, vence en 30 minutos y
  admite 5 intentos. Pedirlo exige clave y un segundo factor recién verificado (desafío de propósito
  `step-up`, mismo verificador del ingreso). Al confirmar se reemplazan `Email`, `NormalizedEmail`,
  `UserName` y `NormalizedUserName` (estos dos siguen igual al correo, R1), se rota el sello y se avisa
  al correo anterior.
- **Rescate del maestro**: cambia el correo con motivo y validación de identidad fuera del sistema.
- **D-10**: `POST /api/admin/users/{id}/reset-password` se retira sin alias, como el repositorio retira
  rutas inseguras. Lo reemplaza `POST /api/admin/users/{id}/send-recovery-link`, que emite el mismo
  token de `RequestPasswordReset` hacia el correo verificado. La pantalla de usuarios cambia el botón.
- **Recuperación de clave**: `forgot` acepta correo o usuario por el mismo resolvedor de R5. El canal
  SMS/WhatsApp queda como `ICanalDeRecuperacion` con una sola implementación, la de correo.

## R12. Retiro de una cooperativa (FR-043)

**Decisión**: `DisableUser` y `RevokeMembership` buscan la cuenta central por `SEC_Users.CentralUserId`
(por correo sólo en filas viejas sin vínculo) y anotan como autor a quien retira (hoy anotan al
retirado). El corte ya ocurre en la siguiente renovación, porque el refresh exige membresía activa
(`RefreshTokenCommandHandler.cs:121`), y en cada petición, porque `SEC_Users` queda inactivo. Una e2e
lo fija.

## R13. Auditoría (FR-044)

**Decisión**:

- Lo que no es de una cooperativa (documento, correo, fusión, rescate) va a la auditoría global
  `{prefijo}_Global`. Lo de una cooperativa (invitación, revisión, corrección por el administrador,
  enlace de recuperación) va a la suya.
- Se emiten explícitamente los eventos `Invitation.Issued.*`, `Revoked` y `Superseded`, que hoy están
  definidos y no se emiten.
- `ADM_DocumentHistory` es de sólo inserción.

## R14. Migraciones (FR-046..FR-049, Principio XII)

**Decisión**: dos migraciones pares, PostgreSQL y SQL Server, aditivas.

- **`IdentidadConDocumento`** (Admin):
  - Columnas en `ADM_CentralUsers`, `ADM_Invitations`, `ADM_TenantMemberships` y
    `ADM_CentralUserLoginAttempts`.
  - Tablas `ADM_DocumentHistory`, `ADM_IdentityCases`, `ADM_EmailChangeRequests` y
    `ADM_TenantIdentityPolicies`.
  - Índices únicos filtrados de documento y usuario.
  - Guarda y luego índice único de correo, y el paso de verificación de R4.
- **`UsuarioDeLaPersona`** (Application, en cada cooperativa vía DbMigrator):
  - Agrega `COR_People.Username` y `SEC_Users.PositionId` / `CentralUsername`.
  - Rellena `Username`. Si dos personas chocan al normalizar (diagnóstico B6), la migración se detiene
    con la lista.
  - Crea `UK_COR_People_Username` y después retira `UK_COR_People_TaxId`.
- **Reversa**: vuelve a crear `UK_COR_People_TaxId` y falla si ya conviven CC y CE con el mismo
  número, lo que el runbook explica. La reversa de la base administrativa deja los datos, porque las
  columnas son nuevas.

**Documento de las cuentas existentes**: una tarea de una sola vez del DbMigrator
(`identidad-documento-desde-cooperativas`) recorre el directorio. Si todas las cooperativas de una cuenta
tienen la misma persona enlazada con el mismo usuario, sube el documento a la cuenta central. Si no,
lo deja en el reporte de conflictos. Ejecutarla dos veces da lo mismo.

## R15. Pruebas

- **Domain**: casos de normalización y derivación en tabla, entre ellos los de la sección de pruebas
  del borrador: cédula con puntos y con `CC`, PA alfanumérico, CC y CE con el mismo número.
- **Application**: los handlers de login, invitación, activación, corrección, fusión y correo.
- **Arquitectura**: `ElUsuarioSeDerivaEnUnSoloSitio`, `LosPrefijosDelUsuarioNoSonPrefijoDeOtro`,
  `NadieBuscaPersonasPorNumeroSolo` y `NadieBuscaCuentasPorCorreoFueraDelResolutor`.
- **Integración**, al final como en las features recientes:
  - una **regresión de la regla de oro**: base con cuentas de antes, migrar, entrar, renovar una sesión
    viva;
  - los flujos de la spec;
  - migrar dos veces.

## R16. Calendario

Una sola entrega antes del 01/12/2026 (D-16) deja unas cuatro semanas. La ruta crítica es:

1. migraciones y regla de oro;
2. invitación y activación;
3. ingreso por usuario;
4. lo de administración (corrección, fusión, correo).

Si el seguimiento muestra que no alcanza, lo primero que se le propondría al dueño mover es la fusión y
el cambio de correo, que no se necesitan para dar de alta a COOFLOPAL. Es una propuesta: hoy la
decisión vigente es una sola entrega.

## Decisiones del dueño tras el MVP (2026-10-28)

- **D-18, invitación con documento obligatorio**: `IssueTenantInvitationCommandValidator` e
  `IssueMasterInvitationCommandValidator` aplican las mismas reglas desde un solo sitio,
  `Invitations/Services/InvitacionConDocumento`: tipo y número (código `Invitation.DocumentRequired`),
  confirmación, nombre y apellido. Se retira la compatibilidad «sin documento, sólo correo» al emitir. Lo
  ya emitido no cambia: aceptar sigue mirando `Invitation.RequiresDocument`, así que una invitación
  pendiente de antes se activa como siempre. El cliente `CooperativasClient` pierde la sobrecarga que
  invitaba sólo con correo. Las e2e que invitan operadores lo hacen con `DocumentoDePrueba` (cédula
  derivada del correo).
- **D-19, política de identidad**: se queda con el guardián de la política de segundo factor
  (administrador de la cooperativa o maestro); no hay permiso nuevo.
