# CLAUDE.md — IngenIA365ERP

## Ubicaciones del Proyecto
- **Proyecto activo**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesWeb\IngenIA365ERP\
- **Constitución del proyecto**: `.specify/memory/constitution.md` (doce principios vinculantes — leer antes de cualquier cambio significativo)
- **Documentación completa**: docs/ (ver docs/INDICE-DOCUMENTACION.md)
- **Proyecto original VB.NET**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\solido.sln
- **ERP.Core (Fase 1)**: D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\Plugins\PluginsComplete\ERP.Core\ (también copiado a `legacy/ERP.Core/`)
- **Base de datos DDL**: database/schema/ — **CONGELADO**, referencia histórica.
  El esquema vivo son las migraciones EF por proveedor
  (`src/Infrastructure/IngenIA365ERP.Persistence.Migrations.*`); esos .sql no se
  aplican ni se mantienen.
- **Scripts migración datos**: database/migration/

## Visión General
IngenIA365ERP es un ERP financiero SaaS multi-tenant para cooperativas colombianas, migrado desde el sistema desktop SOLIDO (VB.NET WinForms) a una arquitectura web moderna.

## Stack Tecnológico
- **Backend**: .NET 10.0.5, Minimal APIs con Carter, CQRS con MediatR
- **Frontend**: Blazor Hybrid MAUI + Web + WebAssembly, Syncfusion 33.2.8 **por paquetes
  de componente** (`Syncfusion.Blazor.Grid`, `.Inputs`, `.Buttons`… en `Shared.csproj`;
  Web y Web.Client sólo `Syncfusion.Blazor.Core`). El meta-paquete `Syncfusion.Blazor`
  **no se referencia**: es una sola DLL de 27 MB con toda la suite que el cliente
  WebAssembly descargaba entera y que hacía tardar ~8 min el publish (desde el
  2026-09-13; hoy los ensamblados Syncfusion pesan 9 MB, 2,4 MB en Brotli). Al usar un
  componente nuevo se agrega su paquete, misma versión en todos. En `App.razor` todo CSS
  y JS va con `@Assets[...]` (URL con huella, `immutable`): sin huella, Cloudflare
  cacheaba 4 h y tras cada despliegue se veían los estilos viejos.
- **BD**: PostgreSQL / SQL Server (transaccional) + MongoDB (auditoría) + Redis (caché)
- **Auth**: JWT RS256, 4 roles built-in, 40 permisos. El segundo factor es
  **obligatorio también para el administrador maestro**: su login devuelve un
  challenge, nunca una sesión directa. Los fallos de autenticación responden
  401, no 422.
- **Identidad con documento (feature 017, 2026-10-29, en develop)**: se entra con **un solo campo,
  «Correo o usuario»** (`identifier`; `email` sigue como alias). Con `@` sólo sirven correos verificados
  (los de toda cuenta que ya entró o aceptó una invitación se dieron por verificados, D-03). Sin `@` se
  busca el **usuario derivado** del documento.
  - **El usuario se deriva en un solo sitio**, `UsuarioDeDocumento.Derivar` (Domain/Common/Identidad): la
    CC va sin prefijo y los demás tipos con el suyo (`CE987654`, `PAAB123`).
  - **El catálogo de tipos es fijo**, `TiposDeDocumento`, sin pantalla: un tipo nuevo es una versión nueva.
    Longitudes D-17: CC y CE 3–10, PPT y PEP 3–15, TI 3–11, PA 3–16; letras sólo en el PA.
  - **Una sola puerta encuentra la cuenta**, `ResolutorDeIdentificador`. Inexistente y clave errada dan el
    mismo mensaje y el mismo tiempo, y el bloqueo se cuenta **por cuenta**, no por correo.
  - **Las personas de cada cooperativa también tienen usuario** (`COR_People.Username`, migración
    `UsuarioDeLaPersona`), y la unicidad pasa a ese campo: CC 123456 y CE 123456 conviven.
  - **Todo lo que busca personas por el número** pasa por `ResolutorDePersonaPorDocumento`: sin tipo y con
    dos candidatos responde `Person.DocumentAmbiguous`. **La reversa de esa migración falla si ya
    conviven** (runbook §5).
  - **Toda invitación nueva exige documento**, dos veces, con nombres, celular, rol y cargo
    (`Invitation.DocumentRequired`; incluye el primer administrador de `with-admin`, D-18 y D-20). El cargo
    sale del catálogo de la cooperativa (`/api/core/positions`) y es informativo, no da permisos.
  - **Al activar se pide el número.** Agotados los intentos, la invitación queda `UnderReview` y el
    administrador la corrige.
  - **Completar el documento no bloquea** salvo desde la fecha que fije cada cooperativa
    (`ADM_TenantIdentityPolicies.DocumentRequiredFrom`, nula por defecto, hora de Colombia). Lo decide
    **`GuardiaDeDocumento`**, en las mismas puertas que `GuardiaDeMetodos`, y a quien ya tiene documento ni
    lo mira. La política la cambian el administrador o el maestro, sin permiso nuevo (D-19).
  - **Correcciones**: corrige el administrador si la persona sólo está en su cooperativa; si está en varias,
    o para fusionar, el maestro (`/api/saas/identities`, `ADM_DocumentHistory`, `ADM_IdentityCases`). Una
    corrección **no cambia la persona de negocio**: cada cooperativa ve la diferencia y la resuelve (D-08).
  - **El correo lo cambia sólo la persona**: `POST /api/auth/step-up` (clave más TOTP o código de respaldo,
    **no passkey**), código al correo nuevo y aviso al anterior. El maestro rescata con `change-email` y
    motivo; la búsqueda del maestro es exacta.
  - **El administrador ya no fija claves**: `POST /api/admin/users/{id}/reset-password` se retiró sin alias
    y en su lugar está «Enviar enlace de recuperación» (`send-recovery-link`, `ICanalDeRecuperacion`).
  - **Los usuarios existentes no notan nada**: no hay claims nuevos ni se rota ningún sello. Lo único que
    se nota es que quien estaba bloqueado al desplegar queda libre, una vez.
  - Dos migraciones aditivas con guarda: `IdentidadConDocumento` (Admin) y `UsuarioDeLaPersona`
    (cooperativa). La tarea `identidad-documento` del DbMigrator rellena los datos y reporta los conflictos.
  - Lo vigilan `ElUsuarioSeDerivaEnUnSoloSitio`, `LosPrefijosDelUsuarioNoSonPrefijoDeOtro`,
    `NadieBuscaCuentasPorCorreoFueraDelResolutor` y `NadieBuscaPersonasPorNumeroSolo`.
  - Recetas: `docs/operaciones/identidad-con-documento.md` y `docs/manual/usuarios-e-identidad.md`. El
    recorrido a mano en web y MAUI (T099) es del dueño.
- **Sesión**: access de 15 minutos y dos límites configurables en la sección `Sesion`
  de la API (`InactividadMinutos` 30, `DuracionMaximaHoras` 12), publicados en
  `GET /api/auth/session-policy` para que el cliente use los mismos números. La
  duración máxima es un tope absoluto desde el ingreso (el refresh rota pero no lo
  corre; pasado, `Identity.RefreshToken.SessionExpired`). La inactividad **el servidor
  la mide entre rotaciones** porque no ve cada petición (`InactivityExpired`); por eso
  la pestaña activa rota sola dos minutos antes de ese límite, y **el corte exacto a
  los 30 lo hace la pestaña**: cinco minutos antes avisa con «Seguir trabajando», a
  cero revoca el refresh y va al login. Una renovación silenciosa **no** cuenta como
  actividad: si contara, la pestaña abandonada se mantendría viva sola. En el cliente
  la sesión la administra **un solo objeto**, `RenovadorDeSesion` (singleton, como
  `ISecureStorage`, porque los handlers HTTP viven en otro scope): renueva cuando al
  access le queda menos de un minuto, ante un 401 inesperado renueva y reintenta una
  vez (cuerpos hasta 8 MB), y una sola renovación en vuelo, porque el refresh rota y un
  segundo canje del mismo token es «reuso» y mata la familia.
  `RenovacionDeSesionHandler` va **primero** en la cadena del cliente `api`. Los dos
  relojes los cuenta `AvisoDeVencimientoDeSesion` en el layout, sin modal y sin
  navegar; `?returnUrl=` devuelve a la pantalla donde se estaba. Hasta el 2026-09-10
  nadie llamaba al refresh y la app expulsaba a los quince minutos; las claves
  `CentralIdentity:AccessTokenLifetimeMinutes/RefreshTokenLifetimeHours` que había en
  `appsettings.json` no las leía nadie y se retiraron. **La cabecera `Authorization` la pone
  sólo el handler**: ningún cliente ni pantalla de Shared la pone a mano
  (`ElTokenDeSesionLoPoneElHandler`; se exceptúan los tokens de desafío y el cierre de sesión).
  Hasta el 2026-09-18 quince clientes tipados (Nómina, Personas, Contabilidad, Permisos…)
  mandaban `CurrentAccessToken` en la cabecera y el handler se apartaba al verla: salían sin
  renovar ni reintentar, y a los quince minutos del último canje decían «La sesión expiró»
  mientras las pantallas con `Http.GetAsync` a secas seguían andando —y al renovar éstas, aquéllos
  volvían solos—. Hoy el handler reconoce el access de la sesión (vigente o recién rotado,
  `RenovadorDeSesion.EsTokenDeSesion`) y lo reemplaza por el vigente.
- **JSON de la API**: los enums **entran por nombre o por número y salen como número**
  (`Application/Common/Json/EnumPorNombreONumero`, registrado en `ConfigureHttpJsonOptions`).
  Las pantallas mandan el nombre («Earning», «Monthly») y los DTOs de Shared leen `int`; hasta
  el 2026-09-18 System.Text.Json sólo aceptaba números y crear un concepto o un plan de nómina
  desde la pantalla respondía un 400 vacío —sin sobre, sin log— antes de llegar al handler. Un
  enum con su propio `[JsonConverter]` (`TipoDeColumna`, que viaja por nombre) queda fuera del
  convertidor global: en STJ el de las opciones pisa al atributo del tipo, y el 2026-09-19
  Reportes de nómina volvió a caer por eso. Un cuerpo que no se puede leer responde **400 con el
  sobre** `Request.BodyInvalid` y el motivo («“Lunar” no es un valor de PayrollPeriodicity.
  Admite: …»): `RouteHandlerOptions.ThrowOnBadRequest = true` en todos los ambientes y el
  manejador de excepciones lo traduce; antes era un 400 vacío en producción y un 500 en Development.
- **Menú**: todo `NavLink` del menú apunta a una página con `@page`
  (`TodoEnlaceDelMenuTieneSuPagina`); una ruta sin página muestra «Página no encontrada» con el
  layout mínimo y parece que la app se sale. Los informes contables (E2) y Beneficiarios no
  tienen enlace hasta que exista la pantalla.
- **Segundo factor**: vive en `ADM_MfaCredentials`, una tabla con discriminador
  (TPH) — no en la columna `ADM_CentralUsers.MfaSecret`. Esa columna **sigue
  escribiéndose** como red de rollback mientras dure el traslado, y la
  verificación cae a ella si no encuentra credencial; el log lo marca como
  `[Mfa.LecturaHeredada]`. Vaciarla es una migración destructiva aparte.
  Hay dos tipos de credencial: TOTP y **passkey (WebAuthn)**. Una persona puede
  tener varias de cualquiera de los dos, y al entrar sirve cualquiera.
  El navegador se toca desde **un solo archivo**, `wwwroot/js/webauthn.js`, y
  todo lo que cruza es base64url. Ese formato no lo elige nadie de aquí: lo
  ponen la especificación y Fido2NetLib, y equivocarse en un campo no da error
  sino un «no se pudo verificar la llave» indistinguible del real. Lo fija
  `ElFormatoDeLaRespuestaWebAuthn`, que comprueba las dos mitades. **No hay
  pruebas de navegador en el repositorio**: el interop se prueba a mano.
  `TwoFactorEnabled` es columna almacenada y **sólo la escribe
  `AspNetCoreIdentityProvider`**, derivándola de `ContarActivasAsync` — que
  cuenta TODAS las credenciales, no sólo las TOTP: cuando contaba sólo un tipo,
  retirar un passkey se llevaba por delante el TOTP.
- **Passkeys**: `RelyingPartyId` va por ambiente (`localhost` en desarrollo,
  `ingenia365.com` en producción) y **cambiarlo invalida todas las llaves ya
  inscritas, sin migración posible**. No tiene valor por defecto a propósito: el
  proceso no arranca si falta. La librería (`Fido2NetLib`) sólo la conoce
  `WebAuthnService`, y una prueba de arquitectura lo fija.
- **Lo que sigue pendiente y NO se ha hecho**: vaciar
  `ADM_CentralUsers.MfaSecret` y retirar el modo compatibilidad de lectura. Es
  destructivo y el Principio XII exige backup y segundo revisor. El orden importa
  y es contraintuitivo — vaciar primero, retirar la lectura después. Ver
  `docs/operaciones/retirada-de-mfasecret.md`.
- **Política de métodos**: cada cooperativa decide **qué** métodos acepta
  (`ADM_TenantMfaPolicies.AllowedMethodsMask`, máscara de bits `MetodosMfa`) además
  de si los exige. La decisión vive en **un solo sitio**, `GuardiaDeMetodos.Evaluar`,
  que consultan las siete puertas que acuñan un token con cooperativa resuelta
  —los dos auto-selects, elegir, cambiar, el ascenso tras inscribir, aceptar
  invitación y **el refresh**. Dos reglas que parecen detalles y no lo son: la
  máscara **sólo muerde si la cooperativa exige** segundo factor, y una máscara que
  lo acepta todo **no mira el método** — sin eso, el despliegue expulsa a la vez a
  todas las sesiones vivas, que no llevan el dato. Nunca se rechaza sin salida: el
  veredicto es «adelante» o «te falta inscribir **esto**», con la lista.
  El método usado viaja en el claim `mfa_method`; ausente significa «no consta», y
  el código de recuperación sella eso mismo a propósito.
- **Recuperación por correo**: apagada por defecto en cada cooperativa
  (`AllowEmailRecovery`). Si el correo puede borrar el segundo factor, el segundo
  factor se degrada al primero, así que las mitigaciones **no son opcionales**: se
  pide desde el desafío —la contraseña ya acertada, no sirve de oráculo—, espera
  las horas que diga la política (mínimo 1, por defecto 24), el aviso trae un
  enlace de cancelar que **no pide nada**, cualquier ingreso correcto la cancela
  sola, y completarla **no devuelve sesión**: recuperar no es entrar. Con varias
  cooperativas manda la más estricta y la demora más larga. El correo sale por
  `IEmailSender` directo, nunca por `SendNotificationCommand` — eso escribe en la
  base de una cooperativa y aquí todavía no hay ninguna elegida.
- **El maestro**: `ADM_PlatformMfaPolicy` (fila única) decide **qué** métodos
  acepta, nunca **si** los exige. Es la única cuenta sin rescate —`ForceMfaReset`
  exige ser maestro, o sea sólo puede rescatarse a sí mismo— así que existe
  `Mfa:PlataformaSinRestriccion`: puesto a `true` ignora la política guardada y
  acepta todos los métodos. Es el rescate; se escribió antes que la política.
- **Cifrado**: el llavero de DataProtection vive en `ADM_DataProtectionKeys`, no
  en el proceso. Cifra los secretos TOTP **y la clave de cada adjunto**. Antes de
  desplegarlo hay que **decidir**, no ejecutar un procedimiento: si no hay adjuntos
  que duelan, aceptar la pérdida es correcto y no cuesta ningún paso manual —la
  clave nueva la genera ASP.NET Core sola. Rescatar las claves pod a pod sólo tiene
  sentido si hay adjuntos que importan, y hay que hacerlo con los pods vivos.
  **En desarrollo nada de esto aplica**: la ejecución por defecto es local —lo dice
  `appsettings.Development.json`—, un solo proceso sin réplicas, y la tabla la crea
  `AutoMigrate` al arrancar. Lo único que cambia en local es que el llavero ahora se
  va con la base administrativa si se recrea. Ver
  `docs/operaciones/llavero-dataprotection.md`.
- **Multi-tenancy**: Una base de datos por cooperativa (constitución v2.0.0, Principio IV).
  El aislamiento es físico. La base administrativa `IngenIA365ERP_Admin` es una sola y
  vive fuera de toda base de cooperativa. Desde la 013 guarda también los **valores nacionales** con vigencia
  (`ADM_NationalValues`: usura, UVR, topes, tipos de organización, Anexo 2) y sus soportes (`ADM_RegulatoryDocuments`),
  que se replican a `COR_` de cada cooperativa.
- **Códigos de catálogo** (desde el 2026-09-12): en todo catálogo el código es la
  nomenclatura de la cooperativa —alfanumérico, en mayúsculas, sin espacios, único en su
  tabla— y nunca un identificador del sistema (para eso están `Id` y `PublicId`). Lo
  fija `CodigoDeCatalogo` (Application/Common/Catalogos): 10 caracteres, 20 en centros
  de costo. En nómina (EPS, ARL, pensiones, cesantías, cajas, causas de retención) es
  `Code`, obligatorio; en Core es `LegacyCode`, opcional (índice único filtrado). Las
  clases ARL siguen numéricas 1..5 porque el motor las traduce. Un duplicado responde
  `Catalogo.CodigoDuplicado` con el nombre del existente, y la pantalla lo consulta
  antes (`GET /api/catalogos/{catalogo}/codigo/{codigo}`, componente `CampoCodigo`).
- **Personas y roles (feature 008, 2026-09-13)**: la persona se escribe desde la interfaz en
  **un solo sitio**, `Components/Personas/PersonaDialog` sobre `PersonasClient`; Empleados y
  Asociados muestran a la persona existente **de sólo lectura** («Editar datos de la persona»
  abre ese diálogo) y con persona nueva registran persona y rol en **un paso**
  (`POST /api/payroll/employees/with-person`, `/api/core/associates/with-person`: un comando, un
  `SaveChangesAsync`, un evento de auditoría). Las banderas `IsEmployee/IsAssociate/IsSalesperson`
  **no están en el contrato** (`PersonInput`): las escribe sólo el handler que crea o retira la
  fila hija; hasta esa fecha `UpdatePersonCommand` sobrescribía las ocho y registrar un empleado
  apagaba «Asociado». Las otras cinco se editan en Personas. El documento es único **incluso
  frente a eliminadas**: `Person.TaxIdDeleted` y `POST /api/core/people/{id}/restore` (misma fila,
  permiso `Core.People.Delete`); una carrera entre dos usuarios se traduce a `Person.TaxIdDuplicate`.
  El **reingreso** de un empleado retirado es una **ficha nueva** (`UK_PAY_Employees_PersonId`
  filtrado a fichas vivas; `by-person` devuelve sólo la viva). `/api/core/people`, `/associates` y
  `/payroll/employees` exigen `Core.People.*`, `Core.Associates.*`, `Payroll.Employees.*`
  (Operador crea y edita; eliminar/restaurar y terminar contrato son del administrador; todo rol
  existente conserva la lectura); el cliente los conoce por `GET /api/admin/permissions/mine`
  (`PermisosDelUsuario`) y `PermissionGate` vive en `Shared` —antes estaba en `Web.Client`,
  `Shared` no lo veía y era inerte—. `GET /api/core/people/{id}` devuelve la persona **completa**:
  antes faltaban 13 campos y editar en Personas los borraba. Receta para sumar módulos (fase 2:
  Vendedores, Proveedores, Clientes, Terceros y los 2 buscadores ad-hoc que quedan):
  `docs/manual/alta-de-persona-desde-modulos.md`. Lo fijan `LaPersonaSeEscribeEnUnSoloSitio`,
  `LosMaestrosDePersonaExigenPermiso` y la e2e `AltaDePersonaEnUnPasoTests`.
  De paso: `PAY_Employees.TerminationCause` era el `varchar(4)` del código SOLIDO y la pantalla lo
  pedía como texto libre (500 con cualquier motivo real); pasó a 120 con validador
  (`MotivoDeRetiroComoTexto`).
- **Contabilidad (feature 009, en la rama `009-contabilidad-niif`, entrega E1)**: contabilidad
  NIIF nueva sobre 29 tablas `ACC_*`; las 33 heredadas se retiran con la migración par
  `ContabilidadNiif`, que es **destructiva con guarda** (falla si `ACC_JournalEntries` o
  `ACC_Documents` tienen filas; marcador `MIGRACION-DESTRUCTIVA-APROBADA` con respaldo y
  segundo revisor) y **vacía `PAY_ConceptDefinitionAccounts`**: tras iniciar la contabilidad
  hay que reparametrizar las cuentas por concepto. Reglas que no se negocian: **un solo
  camino al libro**, `AccountingPoster` (`Application/Accounting/Posting`) —nadie más hace
  `new AccountingDocument`/`JournalEntry` ni toca `VoucherType.NextNumber`; lo vigila
  `NingunModuloEscribeMovimientosFueraDelContrato`—; **no hay saldos guardados** (todo es una
  suma sobre `ACC_JournalEntries` con `IsPosted`); **las reglas viven en la cuenta**
  (módulos habilitados, exige tercero/documento/centro/sucursal, bancaria, de impuesto con
  tarifas) y las evalúa `AccountLineRules`; lo contabilizado **no se edita ni se borra**: se
  reversa, y sólo el módulo dueño reversa lo suyo (`Accounting.Document.ModuleOwned`). «Fecha
  ≤ hoy» sólo aplica al digitar: un módulo fecha según su operación (la nómina, al fin del
  período). El borrador manual se guarda con errores, se valida al salir de cada campo
  (`POST /documents/validate`, errores con `lineNumber`/`field`) y contabilizar es otro
  permiso (`Vouchers.Post`; cuatro ojos opcional por empresa). Toda línea lleva sucursal (la
  propuesta si no viene); el tercero es una **persona** y las entidades institucionales
  (EPS, ARL, fondos, cajas, bancos) se vinculan a la suya (`PersonId`, FR-088). Semillas
  JSON embebidas: PUC solidario —el **CUIF oficial** de la Supersolidaria, formato SIAC 2023-11-03,
  2.110 cuentas, desde el 2026-09-18; antes era una transcripción con 695 códigos, varios
  inexistentes— y comercial (1.869), **pendientes de validar por el contador**; un catálogo
  sembrado se pone al día al cambiar `version` (`SincronizacionDeCatalogo`: en su sitio, sin
  duplicar, y el plan se vuelve a copiar sólo si sigue intacto). Los códigos son regla fija por
  nivel (`LongitudDeAuxiliar`: 2/4/6 exactos; nivel 5 de 7 a 9; nivel 6 de 10 a 12) y **donde el
  catálogo no trae hijos** (127 cuentas y 6 grupos del CUIF: reservas, fondos, provisiones,
  excedentes, contras de orden) la empresa crea **cuentas propias** del nivel siguiente; donde sí
  los trae, no. 69 rubros NIIF, 18 tipos de comprobante, 8 documentos cruce, 30 permisos
  (`Operator` digita y exporta, no contabiliza). El ingreso a cada opción del ERP queda en
  la auditoría con módulo `Navigation` (`RegistroDeAccesos` → `POST /api/audit/access`).
  Entregas: E1 (núcleo, esta rama), E2 consultas/cierres/apertura, E3 los otros seis módulos
  sobre el contrato (sus escritores muertos llevan el marcador `E3 (feature 009)`), E4
  conciliación, impuestos, exógena, activos. Recetas:
  `docs/manual/contabilidad-contrato-de-contabilizacion.md` y
  `docs/operaciones/contabilidad-primer-ejercicio.md`.
  **E2 consultas e informes + presupuesto (rama `009-e2-consultas-presupuesto`, 2026-09-20; US5
  completa y US9 adelantada de E4; US6 y US13 el 2026-09-21, abajo)**: todo saldo es una suma sobre `ACC_JournalEntries` con `IsPosted` por **un solo
  punto de lectura**, `MovimientosContables` (`Application/Accounting/Reports`): alcance de
  sucursal, todos los filtros combinables (`FiltrosDeInforme`), la **apertura siempre es saldo
  inicial**, el **cierre del ejercicio consultado queda fuera salvo `includeClosing`** (los de años
  anteriores siempre cuentan), y la reversa muestra original y espejo. `JerarquiaDelPlan` agrega
  hacia arriba en memoria (no hay saldos guardados, FR-046); los signos van **por naturaleza de la
  cuenta** y el encabezado lo dice. Trece vistas en `/api/reports/accounting/{vista}?format=`
  (`ledger` con `node=` para clase → … → auxiliar → tercero → documento cruce → comprobante →
  línea; `trial-balance`, `journal`, `general-ledger`, `voucher-list`, `third-party-statement`,
  `pending-documents`, `daily-average`, `financial-position`, `income-statement`,
  `equity-changes`, `cash-flow`, `budget-execution`; filtro `niifItem` para llegar del rubro del ESF/ERI al
  libro; rango de hasta 5 años), todas `TablaExportable` con **columnas
  ocultas** (`Clave` que empieza con `_`: `_nodo`, `_cuenta`, `_comprobante`, `_tipo`, `_rubro`)
  que la pantalla y los exportadores omiten; `Accounting.Reports.View` para json y
  `Accounting.Reports.Export` para archivo (`RequirePermissionWhenExporting`, mismo 404), cada
  exportación audita `Accounting.Report.Exported`. Los estados financieros salen del rubro NIIF
  de cada cuenta de movimiento (`SaldosPorRubro`, que mide cada cuenta **por el lado que el rubro
  espera** —clase del código, invertida si `Sign < 0`—, no por la naturaleza de la cuenta: 3510,
  1899 o 6220 descuadraban); el ESF inyecta el resultado del ejercicio en
  `ESF-PT-REJ` y deja las cuentas de orden como memorando; ECP y EFE indirecto se derivan del ESF
  con mapeos fijos (`RubrosNiif`). Presupuesto (`/api/accounting/budgets`, `ACC_Budgets` por
  versión: modificar uno aprobado exige motivo y crea otra versión) sobre cuentas de movimiento, en
  pesos, con alcance de sucursal; la ejecución compara con la vigente y muestra la inicial, y se
  sirve también bajo `Accounting.Budget.View` (`/api/accounting/budgets/execution`). Pantallas en el módulo
  (`/contabilidad/libro-auxiliar` con migas de pan, `/informes?vista=`, `/estados-financieros?estado=`,
  `/terceros?person=` —la vista consolidada del tercero—, `/presupuesto`), enlazadas desde
  `/reportes`; `TodoEnlaceDelMenuTieneSuPagina` ahora también revisa los `NavigateTo` del Centro
  de Reportes (hasta esa fecha cinco tarjetas contables llevaban a «Página no encontrada»). Dos
  defectos que las e2e destaparon fuera del módulo: **`ICurrentUserService.TenantId` es el Id
  interno** de la cooperativa («3»); la auditoría se escribe y se lee por el **PublicId** de
  `ICurrentTenantService`, así que `AccountingAuditEmitter` y `PayrollAuditEmitter` usaban la base
  Mongo equivocada y ninguna exportación ni evento explícito de nómina aparecía en la consola; y
  `COR_Branches.TenantBranchPublicId` (el vínculo sucursal contable → oficina, del que depende el
  alcance de sucursal FR-035) no lo escribía ningún comando: `CreateBranch`/`UpdateBranch` ya lo
  aceptan (una oficina, una sola sucursal: `Branch.OfficeAlreadyLinked`) y la pantalla de Agencias
  (`/maestros/agencias`) lo elige, quita y avisa qué agencias no tienen oficina; la oficina de otra
  agencia sale con su nombre y deshabilitada (`VinculoAgenciaOficina`, 2026-10-30). Una agencia sin
  oficina queda **fuera** del alcance de quien tiene oficinas asignadas (no la ve ni digita en ella);
  quien no tiene asignaciones no tiene restricción.
  **E2 cierre del ejercicio y saldos de apertura (2026-09-21, US6 y US13; con esto E2 está completa
  y las cinco e2e que E1 dejó pendientes —T056, T066, T077, T084, T091— escritas y verdes)**: el
  **cierre** (`CloseFiscalYearCommand`, `POST /api/accounting/periods/years/{year}/close`) exige los
  doce meses cerrados, el anterior cerrado y `AccountingSetup.ResultAccountId`, y genera por el
  contrato un `CI` (`Kind = Closing`) **fechado el 31/12 en período cerrado** —el poster lo admite
  sólo por su clase, y también le perdona «fecha futura» y el alcance de sucursal— que cancela lo
  acumulado en las clases 4 a 7 **por sucursal y centro de costo** y lleva el excedente a la cuenta de
  resultado sucursal por sucursal; sin resultados cierra sin comprobante. `ContextoDeReglas` lleva
  ahora el `DocumentKind`: al cierre no se le exige cuenta activa/habilitada, tercero, cruce ni base
  gravable (cancela lo que ya pasó), a la apertura sólo se le perdona «habilitada para el módulo».
  Reabrir (`/reopen` con motivo) reversa el CI **en su misma fecha y de su misma clase** (el reverso
  es `Closing`: las consultas lo tratan igual y todo sigue neteando), deja los meses cerrados y sólo
  admite el último ejercicio cerrado (`NotLast`); el CI no se reversa desde Comprobantes
  (`Accounting.Document.IsClosing`) y una reversión de cualquier clase tampoco (`ReversesDocumentId`).
  La **apertura** (`Application/Accounting/Opening`, `/api/accounting/opening`, pantalla
  `/contabilidad/apertura`, permiso `Accounting.Opening.Manage`) importa la plantilla —`PlantillaDeImportacion`
  escribe **sólo encabezados en la fila 1** y las instrucciones en otra hoja, porque el exportador de
  informes pone el título arriba y el lector no lo entendería— o se digita con `voucherTypeCode = "AP"`;
  cada fila pasa por las reglas de cuenta y **con un error no se guarda nada** (422 `Opening.Invalid`
  con `row`/`column`), el descuadre no es error de importación, el borrador queda `Kind = Opening`
  **fechado la víspera del primer período y sin período**, se contabiliza por `/documents` (una sola
  vigente: `Opening.AlreadyExists`; `PostDocumentCommand` la referencia en `OpeningDocumentId`) y su
  reversión es también `Opening` de la misma fecha y suelta la referencia. Las e2e que cambian todo el
  libro corren en **cooperativas aisladas del mismo host** (`ContabilidadE2E.CooperativaAisladaAsync`:
  «cierre» con primer ejercicio 2025, «apertura» con 2026), no en la compartida. Receta: runbook §7a.
  **Implantación (2026-09-22, pedido del dueño al implantar COOFLOPAL)**: (1) **carga masiva de
  auxiliares** (`ImportAccountsCommand`, `GET/POST /api/accounting/accounts/template.xlsx|import`,
  botones en `/contabilidad/plan-de-cuentas`) con la mecánica de la apertura —plantilla con
  encabezados en la fila 1, todo-o-nada, `data.errors[] { row, column }`— y **las mismas reglas que
  crear una a una**, porque reusa `CreateAccountCommandHandler.AplicarReglasAsync`: el padre es la
  cuenta viva cuyo código es el prefijo más largo (una propia y su auxiliar pueden ir en el mismo
  archivo: se ordenan por código), un código que ya existe **actualiza** —con movimientos sólo el
  nombre (`Account.Locked`), del catálogo nunca (`FromCatalog`)— y una cuenta que no recibe
  movimiento no admite reglas. (2) **la apertura se fecha donde el corte real de SOLIDO**: la
  víspera del primer período es sólo la propuesta y la fecha se mueve hasta el fin del primer
  ejercicio, nunca a un mes cerrado (`Opening.DateOutOfRange` / `.DateClosed`, en
  `AccountingPoster.FechaDeAperturaInvalidaAsync` para que el contrato y el borrador digan lo mismo);
  su `PeriodId` sigue **nulo** aunque la fecha caiga dentro de un período, que es lo que la mantiene
  como saldo inicial y fuera del cierre mensual. El borrador se edita entero hasta contabilizarlo
  (líneas por `PUT /documents/drafts/{id}` con tipo `AP`, fecha, descartar) y **volver a importar
  reemplaza sus líneas** conservando el mismo comprobante (las viejas quedan de baja, Principio XI).
  **E3 cerrada y E4 (2026-10-30, rama `claude/009-e4-contabilidad-8d71c2`, sin merge)**: E3 la absorbieron las features 012–015 (ningún
  escritor con el marcador quedó; sólo faltaban el enlace del resumido de Recaudo y validar `COR_Banks.AccountingAccountCode` como
  cuenta de Tesorería). La **conciliación bancaria es de Tesorería** (enmienda E-04 de la 015). **Activos** (`Application/Accounting/Assets`,
  `/contabilidad/activos`, módulo `ACT`, tipo `DP`): **las cuotas pendientes no se guardan** —`CalculoDeDepreciacion` (puro) reparte el
  neto en los meses que quedan, así cambiar vida útil o residual es prospectivo y un mes sin correr se acumula—; una corrida por período,
  en orden, sólo se reversa la última con su período abierto (la reversada y sus cuotas quedan de baja lógica `Reversed` para liberar el
  índice único); el activo con historia entra con `OpeningAccumulated`/`OpeningElapsedMonths`. **Impuestos** (`Application/Accounting/Taxes`):
  `TaxKind` gana `VatWithholding` e `IcaWithholding` (los certificados salen sólo de cuentas de retención; la matriz de Inventario las
  acepta para ReteIVA/ReteICA), vistas `withholdings`, `vat`, `ica`, `gmf`, `tax-form/{code}`; formularios por prefijos con selectores
  `Base|Amount|Debit|Credit|BaseDebit|BaseCredit` y 350/300 sugeridos por catálogo (semilla JSON validada); **certificados** con consecutivo por clase,
  huella del libro (desactualizado → reexpedir anula el anterior), PDF y envío, **sin los módulos de `AccountingSetup.CertificatesExcludedModules`**
  (defecto `CAR`: Captaciones certifica lo suyo). **Exógena** (`Application/Accounting/Exogenous`): formatos por año con la disposición
  como dato (`ACC_ExogenousFormats.LayoutJson`), semilla embebida `exogena-2026.json` (1001, 1003, 1005–1010, 1012; validada por la contadora el
  2026-10-31; la 1019/1020 son de la 014 y la 2276 no está) según el catálogo, menores cuantías, inconsistencias bloqueantes y avisos,
  Excel y XML `Dmuisca_…` ISO-8859-1 guardado como adjunto `ExogenousRun`. **Se siembran la primera vez y cada empresa los
  ajusta** (2026-10-31): `InitializeAccountingCommand` siembra los formularios y la exógena de los años con semilla; desde ahí, en pantalla
  (formularios, formatos nuevos con su disposición, conceptos y cuentas) o con el **plano** (`/forms/plano.xlsx|import`,
  `/exogenous/formats/{year}/plano.xlsx|import`: se baja lleno, se sube con la mecánica de la carga de auxiliares, todo o nada, y cada
  formulario o formato del archivo se reemplaza por la misma puerta que la pantalla). Una migración aditiva, `ContabilidadSatelites`. Receta:
  `docs/manual/contabilidad-satelites.md`; runbook §7d.
- **Adjuntos**: se guardan por `IBlobStore`, que tiene dos implementaciones y se elige con
  `AttachmentStorage:Provider`: `Local` (disco, desarrollo) y **`S3`** (`S3BlobStore`, 2026-09-22, los
  tres ambientes del clúster). **El archivo no pasa por el servidor** (feature 011, entrega E3,
  2026-09-23): una persona pide una autorización firmada (`POST /api/attachments/uploads`: clave, tipo,
  tamaño exacto y huella, 5 minutos), el navegador sube directo al bucket con
  `Shared/wwwroot/js/adjuntos.js` —los bytes nunca cruzan a .NET; nada de `InputFile`— y `/confirm` revisa
  lo que llegó con `FirmaDeContenido` sobre los primeros 8 KiB: `Available`, `Rejected` (motivo; el objeto
  va a la papelera) o, vencida la autorización sin objeto, `Incomplete` (se reintenta sobre la misma
  fila y clave con `/upload-url`). Bajar es un enlace firmado de 60 s (`/download-link`, auditado) que
  obliga a guardar con nombre y tipo; PILA y dispersión tienen el suyo con su `charset`. Lo que generan
  los módulos se guarda tal cual, `Direct` y ya `Available`. Hay **dos formatos**: `Direct` (todo lo
  nuevo, cifrado por el bucket, SSE-S3) y `AppEncrypted` (lo escrito antes en DEV y QA, cifrado por
  `AttachmentEncryptionService` y DataProtection), que **no se migra** y se sigue bajando por la API
  (`GET /api/attachments/{id}`, `…/file`); a un `Direct` esas rutas le responden 409
  `Attachments.UseDownloadLink`. Tamaño, tipo y vacío los responde el **handler** (`Validation.Attachments.*`,
  400, `data.maxBytes`), no el validador. Ninguna ruta de adjuntos recibe el archivo salvo `local-blob`
  (`LosAdjuntosNoPasanPorElServidor`). **El bucket necesita CORS** para los orígenes web (lo pone
  `crear-bucket-adjuntos.ps1`); sin él la subida falla en el navegador aunque la API autorice.
  El bucket de adjuntos (`ingenia365-erp-attachments`) es **otro** que el de respaldos (aquél tiene
  Object Lock a 40 días). La clave es `{prefijo del ambiente}/{cooperativa}/{aaaa}/{mm}/{guid}.bin` y
  **el prefijo no se guarda en la base** (`StoragePath` lleva la referencia sin él), así se puede mover
  el prefijo o el bucket sin invalidar lo escrito.
  **Nada se borra solo (feature 011, rama `011-adjuntos-s3-prefirmadas`, entrega E1, 2026-09-23)**:
  borrar es el acto de una persona con `Attachments.Delete`, auditado, y `DeleteAttachmentCommand`
  retira **primero el objeto y después la fila** (si la baja falla, reintentar completa; al revés
  quedaba un objeto vigente sin nada que lo nombrara). La credencial del ERP no borra **versiones**:
  el objeto queda en la **papelera de 90 días** (regla de ciclo de vida `papelera-90-dias`, la única
  purga automática, y sólo alcanza a lo ya borrado); recuperarlo o suprimirlo por Habeas Data son
  recetas de soporte con la credencial administrativa, no botones. Hasta ese día la documentación
  prometía un «GC programado» que nunca existió y borrar sólo daba de baja la fila;
  `NadieBorraAdjuntosPorSuCuenta` fija quién puede llamar a `IBlobStore.DeleteAsync`. Las reglas por
  dueño viven en **un solo sitio**, `AdjuntosDeModulo`: lo que genera un módulo (definitiva,
  dispersión, PILA) no se borra ni recibe subidas; el soporte de un comprobante exige
  `Accounting.Vouchers.View` para leerlo, `Vouchers.Create` y que el comprobante exista para subirlo,
  y **no se borra si está contabilizado o reversado** (`Attachments.OwnerLocked`; antes sólo la
  pantalla escondía el botón —ahora lo decide `canDelete` del servidor—); cualquier otro dueño no
  admite subidas todavía. Descartar un borrador con soportes pide confirmarlo
  (`Accounting.Document.HasAttachments`, `?deleteAttachments=true`). `POST /api/attachments`
  (multipart) **se retiró sin alias**: permitía colgar un archivo de cualquier dueño. El grupo lleva
  el limitador de concurrencia `adjuntos` (32 + 64 en cola por réplica, 429 `Attachments.Busy`).
  Con `Provider = Local` y fuera de Production el almacén local **imita a S3** con tokens de
  DataProtection y rutas anónimas `/api/attachments/local-blob/{token}`. **E2, credenciales temporales**
  (IAM Roles Anywhere, sin costo): **activo en los tres ambientes** (DEV y QA desde el 2026-09-23,
  producción desde el 2026-09-24 con `release e0c97b9`; no queda ninguna llave permanente). La API toma una sesión
  de una hora del sidecar `aws_signing_helper serve` (en `127.0.0.1:9911`, imitando al IMDSv2), con
  un rol por ambiente acotado a su prefijo, sin listar ni versiones. Piezas:
  - `tools/scripts/crear-certificados-adjuntos.ps1`: CA propia fuera del repo y del clúster, un certificado
    por ambiente que vence en un año; al instalar reinicia la API porque el sidecar lee el certificado sólo
    al arrancar;
  - la pila `docs/operaciones/plantillas/adjuntos-roles-anywhere.yaml`;
  - el componente de GitOps `components/adjuntos-roles-anywhere`, **sin sondas**, porque `serve` sólo
    escucha en 127.0.0.1;
  - `probar-credencial-adjuntos.ps1`: 14 comprobaciones de permisos desde un pod de prueba;
  - `revocar-certificado-adjuntos.ps1`: la CRL.

  La receta del paso a producción, con su tropiezo, en `docs/operaciones/despliegue-adjuntos-directos.md`. Falta la verificación en MAUI (E4);
  `specs/011-adjuntos-s3-prefirmadas/`. Hasta ese día los tres ambientes escribían en un PVC `local-path` sin
  redundancia, fuera de los respaldos y `ReadWriteOnce` —con un segundo nodo, una de las dos
  réplicas de la API no habría podido montarlo—; se migró con el volumen vacío en producción, y el PVC `erp-attachments` se retiró de los tres
  ambientes el 2026-09-24 (vacíos; GitOps `431092b`). El
  health check `blobstore` **escribe y borra** un objeto (listar no prueba que se pueda escribir) y
  se lo pregunta al almacén (`IBlobStore.ProbarAsync`), no al disco. Receta:
  `docs/operaciones/adjuntos-en-s3.md`.
- **Reportes**: QuestPDF (16 reportes)
- **Nómina (feature 005)**: el cálculo es un **motor puro en Domain**
  (`Payroll/Calculation/PayrollCalculationEngine`) que recibe todo por parámetro
  —período, empleado con su historial de salarios, novedades, definiciones de
  conceptos y parámetros legales vigentes— y devuelve líneas con explicación. **No hay
  un solo valor legal escrito en el código**: salario mínimo, auxilio, UVT,
  porcentajes y tablas viven en `PAY_PayrollLegalParameters` con vigencia, y la prueba
  de arquitectura `LaNominaNoTieneValoresLegalesFijos` sólo admite en `Domain/Payroll` y
  `Application/Payroll` los literales decimales `0, 1, 2, 0.5, 12, 15, 30, 100, 360`.
  Cinco formas de cálculo (valor fijo, porcentaje sobre base, cantidad × unidad, tabla
  por rangos, suma de conceptos), sin fórmulas libres. Los **casos dorados** son JSON en
  `tests/IngenIA365ERP.Domain.Tests/Payroll/Calculation/Casos/` con valores calculados a
  mano. Las corridas (`PAY_PayrollRuns`, `…RunEmployees`, `…RunLines`, bajo
  `Entities/Payroll/Transactions`) son **inmutables** (Principio XI): recalcular crea
  una versión y deja la anterior `Superseded`; aprobar genera el comprobante `NM` en la
  misma transacción; reversar deja un asiento espejo y reabre el período. La semilla
  deja plan por defecto, 40 conceptos, 33 parámetros con vigencia 2026, las cinco clases
  de riesgo ARL y el `NM`; lo que no deja (cuentas por concepto, período contable, la
  clase ARL de cada ficha) está en `docs/operaciones/nomina-primer-periodo.md`. La ficha
  guarda la **fila** de `PAY_WorkRiskRates`, no la clase: la clase es su `Code`, y hasta
  el 2026-09-11 el cargador tomaba el Id como clase y nadie sembraba la tabla ni la
  pantalla dejaba elegirla, así que toda liquidación salía «sin clase de riesgo ARL». La
  ficha también guarda fondo de cesantías (`SeveranceFundId`, entero desde el 2026-09-12)
  y caja de compensación (`FamilySubsidyId` → catálogo `PAY_FamilyCompensationFunds`,
  sin semilla). La **tabla de retención propia de un plan** (`/nomina/parametros-retencion`,
  `PAY_WithholdingParameters.PayrollPlanId`, desde el 2026-09-19) entra al cálculo por
  `TablaDeRetencionDelPlan`: si el plan del período tiene tramos, reemplazan a
  `RETEFTE_TABLA_UVT` sólo para esa corrida; sin tramos, la tabla legal. Hasta entonces la
  pantalla escribía sobre la «empresa nómina» del legado y la liquidación no la miraba. La **fecha de ingreso** se corrige desde la ficha (`UpdateEmployeeCommand.HireDate`,
  nula = no cambia) mientras no haya nómina aprobada del período, novedad en período cerrado
  antes ni cambio de salario anterior (`Employee.HireDateLocked`); el cambio de salario inicial
  se mueve con ella. Hasta el 2026-09-18 el PUT no la llevaba y la pantalla la mostraba editable.
  **Feature 006 (2026-09-12)**: periodicidades `TenDay=10` y `Weekly=7` (el valor del enum ES la
  base de proporción); cada período lleva `SubPeriodNumber` e `ImputationYear/Month`
  (`PeriodCalendar` los propone y valida la duración); las recurrentes tienen `ApplyOn`
  (cada período / primero / último del mes; **la misma orden no se registra dos veces**:
  `RecurrenteRepetida` rechaza otra recurrente activa del mismo empleado y concepto con vigencia
  cruzada si el concepto no admite repetirse, o idéntica en cantidad y valor si lo admite; el
  materializador aplica la misma regla a las ya registradas —entra la más antigua, el cálculo avisa—
  y **no vuelve a generar una novedad anulada por una persona** en ese período, sólo la anulada por
  el descarte del borrador. El 2026-09-19 en producción quedó una deducción registrada dos veces porque
  «Nueva recurrente» abría con el buscador y los resultados del empleado anterior —los tres diálogos de
  Novedades comparten `_buscarEmpleado`/`_empleadosEncontrados` y ése no los limpiaba—, salía doble en
  cada cálculo y anularla no servía); «Descartar borrador» deja la corrida `Superseded`
  con `DiscardedAt/By/Reason` y el período en `Open`; el centro de reportes
  (`/api/reports/payroll/{vista}?format=`) produce `TablaExportable` y la exporta con
  ClosedXML, OpenXML y QuestPDF desde `API/Reports/Exportadores`. El **cálculo preliminar** anterior
  (`POST /api/payroll/process`, salud y pensión fijas al 4 %) se retiró sin alias.

- **Nómina (feature 010, entrega N1, rama `010-nomina-prestaciones-pila-dian`, 2026-09-21)**: prima
  de servicios, cesantías e intereses del año, vacaciones y liquidación definitiva por retiro. Las
  corridas llevan **`Kind`** (`Ordinary`, `ServiceBonus`, `Severance`, `Vacation`, `Settlement`),
  `CutoffDate`, `PayDate` y período nulo en las especiales; el cálculo es **otro motor puro**,
  `Domain/Payroll/Settlements/SettlementCalculationEngine` (casos dorados en
  `Domain.Tests/Payroll/Settlements/Casos/`), sobre el mismo `Explanation` y las mismas listas
  `Required` de parámetros por proceso (`GET /api/payroll/legal-parameters/missing?process=`).
  **Un ciclo común** en `Application/Payroll/Settlements/Common` (`SettlementRunPersister`,
  `SettlementRunWorkflow` con gancho `antesDeGuardar`; `SettlementAccountingPoster` con
  `SourceType` por tipo, comprobante fechado al **corte**, D-04) y una ruta, un permiso y una
  pantalla por tipo (`/nomina/prima`, `/nomina/cesantias-anuales`, `/nomina/vacaciones`,
  `/nomina/liquidacion-definitiva`); las rutas ordinarias `approve/reverse/discard` rechazan
  `Kind != Ordinary` (`Payroll.Settlement.UseSettlementRoute`). Reglas que no se negocian:
  las **políticas de la empresa** viven en `PAY_CompanyPolicies` **con vigencia** (11 claves de
  `CompanyPolicyKeys`; `PayrollPolicyReader` es el único lector y la fila sembrada manda sobre
  `COR_SystemSettings`, por eso `NominaE2E` abre la vigencia `AllowSameUserApproval`); toda
  liquidación se contabiliza **contra la provisión acumulada** del empleado y la diferencia va
  al gasto o se libera (`*_AJUSTE_PROV`; `ProvisionBalanceReader` informa siempre las cuatro
  provisiones, en cero si no hay historia: si no, la provisión quedaba en negativo); el
  **saldo inicial** de prestaciones es la **fila vigente**, no la suma de ajustes; **un solo
  pagador del último tramo** (D-29): el retirado con definitiva aprobada dentro del período no
  entra a la ordinaria, la definitiva liquida el tramo con las novedades del período abierto y
  los dos borradores se marcan `Stale` entre sí; las vacaciones se pagan anticipadas por
  **días del calendario comercial** (D-01, D-31) y la ordinaria recibe `AUSENCIA_VACACIONES`
  informativa (sin período que la cubra, aprobar responde `PeriodMissing`); la definitiva
  propone los **descuentos de Cartera** (crédito y libranza por `DeduccionAlRetiroModo`; cuotas
  causadas con la regla `ApplyOn`) que sólo se bajan con motivo auditado, recauda por
  obligación dentro de la misma unidad de trabajo, cierra la ficha y genera el documento para
  firma; `POST /api/payroll/employees/{id}/terminate` **se retiró sin alias**. Semillas:
  16 conceptos (`WellKnownConceptCodes.SettlementOnly`, sin cuentas), ~40 parámetros con
  `Source`, 9 motivos de retiro (Order 72), festivos Ley 51 2026–2028 (74), políticas (75);
  los permisos (`Payroll.ServiceBonus/Severance/Vacations/Settlements/BenefitBalances/CompanyPolicies/Holidays`
  y los de N2–N4) los siembra la API al arrancar, **no el DbMigrator**. Dos migraciones
  aditivas (`NominaPrestacionesYDian`, `SettlementDeductionsUnicosEntreVivas`). Receta:
  `docs/manual/liquidaciones-especiales.md`; runbook `docs/operaciones/nomina-primer-periodo.md` §4d.
  N3 (nómina electrónica: servicio central **sin estado**, modo «software propio» de cada
  cooperativa primero) sigue pendiente.

- **PILA y procedimiento 2 (feature 010, entrega N2, misma rama, 2026-09-21)**: la planilla de
  aportes (Res. 2388/2016, archivo tipo 2, planilla E) y el porcentaje fijo semestral del art. 386.
  **El layout es dato versionado**, un JSON embebido con vigencia
  (`Application/Payroll/Pila/Layouts/at2-v30-2026-07-24.json`, 22 campos / 358 posiciones y 98 / 693;
  `PilaLayoutCatalog` elige el vigente por fecha): una versión nueva del anexo es un archivo nuevo,
  no lógica. Todos sus campos llevan `verified = false` hasta que el dueño lo coteje con el anexo
  v30 y una planilla pagada (T094); mientras tanto **sí es vigente** y generar deja la alerta
  `Pila.LayoutSinCotejar` (D-43). **Otro motor puro**, `Domain/Payroll/Pila/PilaBuilder` (+
  `PilaValidator`, `PilaWriter`; casos dorados en `Domain.Tests/Payroll/Pila/Casos/` con el archivo
  `.esperado.txt` byte a byte, regenerable sólo a propósito con `PILA_ESCRIBIR_ESPERADO=1`): una
  línea base por cotizante y una por cada novedad con IBC propio (IGE, LMA, VAC, SLN, IRL), ING y
  RET en la misma línea, IBC ≥ 1 SMMLV proporcional y ≤ 25, integral al 70 %, al peso superior,
  aportes al múltiplo de 100 superior, ARL 0 en IGE/LMA y por política en VAC, SLN sólo pensión
  del empleador, FSP con la tabla vigente o **la de transición según la ficha** (Ley 2381 desde
  2027-04), exoneración del art. 114-1 = política **y** lo **devengado** bajo 10 SMMLV (no el IBC).
  Sin valores legales en el código: `PilaParameterCodes.Required` con vigencia. Datos nuevos:
  `PilaCode` (6, admite guion) en EPS, pensiones (+ `IsAccai`), ARL, cesantías y cajas —distinto
  del `Code` de la cooperativa—, `PAY_PilaSettings` (clase de aportante, forma, ARL, operador,
  DIVIPOLA y actividad de la sede). Reglas: **validar no guarda** y lista las inconsistencias con
  la taxonomía del operador (bloqueante / alerta), campo, empleado y ruta; **generar con
  bloqueantes deja una generación `Validated` sin archivo** con las inconsistencias guardadas;
  con alertas exige reconocerlas; regenerar crea la versión siguiente y deja la anterior
  `Superseded` con su archivo; el **cuadre** (FR-027) compara el archivo con los conceptos de
  aportes de las corridas del mes por subsistema **antes de descargar** y con diferencia la
  descarga exige reconocerla —una diferencia de redondeo en la ARL es normal: la ordinaria
  redondea al múltiplo más cercano y la planilla al superior—; marcar cargada anota el radicado y
  el período ya no se regenera (planillas N y A se digitan en el operador). El **procedimiento 2**
  es `Domain/Payroll/Withholding/FixedRateCalculator` sobre la misma `DepuracionDeRetencion` y
  `RangeTableLookup` de la ordinaria: doce meses anteriores (prima sí, cesantías e intereses no),
  aportes reales, secuencia por política `P2SecuenciaDepuracion`, ÷ `RETEFTE_P2_DIVISOR` o los
  meses de vinculación, tabla vigente o del plan; aprobar **cierra** la vigencia anterior de la
  ficha la víspera y abre la nueva con `Origin = Calculated` (R8, D-44: `SetEmployeeWithholding`
  también cierra en vez de reemplazar). Rutas `/api/payroll/pila` (`Payroll.Pila.*`) y
  `/api/payroll/withholding-rates` (`Payroll.WithholdingRate.*`); pantallas `/nomina/pila` y
  `/nomina/retencion-procedimiento-2`; vistas `pila-lineas`, `pila-cuadre`,
  `pila-inconsistencias`, `retencion-p2`. Migración aditiva `NominaPilaYNominaElectronica` (trae
  también las cuatro tablas de N3, D-12). Lo que sigue siendo del dueño: cotejar el layout y pasar
  el `.txt` por el validador de Aportes en Línea (SC-004), cargar los códigos PILA y confirmar la
  secuencia del procedimiento 2 (8h). Recetas: `docs/operaciones/pila-primera-planilla.md`,
  `docs/manual/retencion-procedimiento-2.md`.

- **Dispersión bancaria (feature 010, entrega N4, misma rama, 2026-09-21)**: la nómina paga por
  archivo plano y «marcar enviado» deja pagados a todos los del archivo en una transacción
  (`PayrollPayment` con `BankDisbursementFileId`, medio `Transfer`, la misma referencia; la corrida ya
  no se reversa). **El formato es un dato de Core ligado al banco, no de nómina** (D-42, pedido del
  dueño para que los planos de otros bancos y los pagos de tesorería y contabilidad usen lo mismo):
  `COR_BankFileFormats`/`COR_BankFileFormatFields` con `BankId` nulo = genérico, `Scope`
  (`PayrollDisbursement`, `SeveranceDeposit`, `SupplierPayments` reservado) y vigencia; **un solo motor**,
  `Application/Common/BankFiles/FlatFileWriter`, que no conoce la nómina —recibe contexto (empresa,
  cuenta origen, lote) y líneas con orígenes genéricos `Payee*`/`Amount`/`Concept`; los `Employee*`,
  `NetAmount`, `PaymentConcept` del contrato son sinónimos—; nómina aporta `PayrollDisbursementLines` y
  guarda sus archivos inmutables en `PAY_BankDisbursementFiles`/`Lines` (adjunto no borrable con
  SHA-256, subido **antes** de guardar la fila); la consignación de cesantías por fondo ya escribe con
  el mismo motor. Reglas: dos formatos activos del mismo banco y ámbito no se cruzan en el tiempo
  (los genéricos tampoco entre sí, `Core.BankFileFormat.Overlaps`); uno que ya generó archivos **no
  cambia de estructura** (`InUse`: se cierra la vigencia y se crea la versión nueva); la cuenta origen
  no va en el formato sino que se elige al generar entre las cuentas bancarias del plan
  (`GET /api/accounting/accounts/bank-accounts`) y debe ser del banco del formato; un empleado va a lo
  sumo a un archivo no anulado de su corrida; un archivo `Sent` no se anula (las marcas se retiran una a
  una). **D-10 resuelto**: `COR_Banks.TransferCode` **es** el código ACH (el `codtras` de SOLIDO); sin él
  el empleado queda en pendientes (`BankCodeMissing`), igual que sin cuenta (`NoBankAccount`). Rutas
  `/api/core/bank-file-formats` (`Core.BankFileFormats.View/Manage`) y `/api/payroll/disbursements`
  (`Payroll.Disbursement.View/Generate/MarkSent`); pantallas `/maestros/formatos-bancarios` (campo a
  campo o JSON pegado, vista previa con una corrida real) y `/nomina/dispersion`; botón «Archivo de
  dispersión» en las cinco relaciones de pago; vista `dispersion` del centro de reportes. Semilla
  `CSV-GENERICO` (Order 76); `AVVILLAS-1` inactivo y sin campos sólo si hay un banco «VILLAS»: el layout
  real de AV Villas lo aporta el dueño (T147) y se carga como dato. Migración aditiva
  `NominaDispersionBancaria` (su scaffold arrastró un cambio de índice de vacaciones ya hecho y se
  limpió antes de salir). Receta: `docs/manual/dispersion-bancaria.md`; runbook §4e.

- **Inventario comercial (feature 012, rama `012-inventario-comercial`, entrega I1, 2026-09-26; rama
  local, sin merge ni despliegue)**: un módulo comercial nuevo en reemplazo del traslado a medias de
  SOLIDO, por entregas (I1 núcleo; I2 integración con Contabilidad; I3 ventas y POS; I4 documentos
  electrónicos DIAN; I5 compras completas y costeo avanzado; I6 comercio ampliado; IC crédito con
  Cartera, bloqueada por D-02). **I1 construyó**: catálogo (unidades con conversiones, categorías,
  marcas, grupos contables con vigencia por producto, productos con códigos de barras e impuestos,
  causas de ajuste, canales), bodegas con ubicaciones y **bodega de tránsito por sucursal**, kardex y
  existencias, costo promedio ponderado (cooperativa o bodega), períodos con cierre y reapertura,
  saldo inicial y **activación bodega por bodega** con cifras de referencia (`/inventario/cifras-de-referencia`; ninguna pantalla nombra al sistema anterior) y comparativos, compra directa,
  recepción, factura y notas del proveedor, devolución y eventos RADIAN, traslados en dos pasos con
  diferencias aprobadas, movimiento entre ubicaciones, conteo físico con foto y ajuste aprobado, reorden
  y quiebre, nueve vistas en `/api/reports/inventory/{vista}` (`kardex`, `stock`, `valuation`,
  `legacy-comparison-kardex`, `legacy-comparison-valuation`, `radian-events`, `count-differences`,
  `documents`, `reorder-alerts`), vendedores como rol (`RolDeVendedor`, `/ventas/vendedores`), y la
  **plataforma** en `Application/Common` que no sabe de inventario: bandeja de salida de mensajes
  (`COR_IntegrationMessages`), ejecución por cooperativa (`IEjecutorEnCooperativa`, actor «Proceso de
  integración», `ProgramadorDeTareas`, arrendamientos `COR_BackgroundLeases`), idempotencia
  (`Idempotency-Key` → `COR_OperationKeys`), parámetros con vigencia (`COR_ParameterVersions`, un
  lector y un escritor), aprobaciones multinivel con montos por permiso (`COR_Approval*`,
  `SEC_PermissionAmountLimits`, aprobador presente con passkey o TOTP), alcance por bodega, alertas
  (`COR_Alerts` sobre `COR_Notifications`), catálogo tributario de Core, perfil tributario de la
  persona, DIVIPOLA, dieciséis plantillas de importación con revisión previa y auditoría con entrega
  garantizada y **cadena de sellos**. Tres migraciones pares: `RetiroDelInventarioHeredado`
  (**destructiva con guarda**: borra 23 tablas `INV_*` heredadas, conserva `INV_Salespeople`, se niega
  si alguna tiene filas sin la fila `COR_SystemSettings` `INV.RetiroHeredado.Aprobado`; diagnóstico del
  2026-09-25: 0 filas en todas las bases de DEV, QA y PDN), `PlataformaParaInventario` (19 tablas de
  plataforma y columnas en `COR_People`, `COR_Cities`, `COR_Branches`, `COR_Notifications`) e
  `InventarioComercialNucleo` (39 tablas `INV_`, `pg_trgm` para la búsqueda). Semillas Order 77–83
  (unidades, tipos de bodega, causas, un tipo de documento por clase con su consecutivo, tributario,
  DIVIPOLA, tipos de alerta); los permisos `Inventory.*` los siembra la API al arrancar, enteros desde
  I1. Rutas de pantalla `/inventario/*` (31), `/compras/*` (9), `/ventas/canales`, `/ventas/vendedores`,
  `/maestros/impuestos`. Reglas que no se negocian:
  **Inventario no escribe asientos ni conoce cuentas** (Q2 B): no lee ni escribe tablas de
  Contabilidad ni de Cartera (`InventarioNoConoceContabilidadNiCartera`); lo que cruza, cruza por
  **mensajes nacidos en la misma transacción** que el documento, por el único escritor
  `EmisorDeMensajes`, que nunca guarda (T7–T12); el mensaje lo origina la persona, nunca el proceso.
  En I1 **no hay despachador**: los mensajes quedan `Pending`/`InBatch` hasta I2, y el modo de paso
  (`EnLinea`, `PorLotes`, `NoPasa`) se sella al confirmar. **Sin contabilidad iniciada el defecto es
  `NoPasa`** (`ModoDePasoVigente`, decisión del dueño del 2026-09-26; lo pregunta
  `IContabilidadParaInventario.SinIniciarAsync`): con el defecto «en línea» una cooperativa nueva no
  confirmaba nada; un modo guardado a propósito se respeta. **Todo lo que mueve inventario es un
  `INV_Documents`** con su clase (34, fijas en `ClasesDeDocumento`, no son columna) y un tipo de la
  cooperativa; **un solo flujo de confirmación**, `ConfirmacionDeDocumento` (relectura → aprobación →
  guardia fiscal y validación previa → cerrojo → efecto de la clase → número → mensajes → un
  `SaveChanges`), con una estrategia `IEfectoDeClase` por clase. **El kardex es un hecho y las
  existencias son proyección**: `RegistroDeKardex` es su único escritor (más la reconstrucción, que no
  toca el kardex; `NadieEscribeElKardexFueraDelRegistro`), rechaza todas las líneas que no caben antes
  de escribir, y la verificación nocturna `inventario.integridad` compara. **Cerrojo pesimista en orden
  fijo** (setup → bodegas → orígenes → costo/existencias/detalle → numeración, por `Id`, igual en los
  dos motores) **y la numeración al final** por `Numerador` (`SoloElNumeradorNumera`; el borrador y lo
  que queda en aprobación no consumen número) (T15, T16). Lo confirmado **no se edita ni se reversa**:
  la **anulación es un documento nuevo** (`Voiding`, con su fecha, al costo del original, con
  `AjusteDeCostoReconocido` si la entrada ya promedió; `LoDeInventarioNoSeReversa`). Retroactivo
  rechazado en I1 salvo el saldo inicial de una bodega `NotActivated` y los ajustes de conteo
  (excepción de puesta en marcha, caso dorado 17). Precisión por alias (`PrecisionDeInventario`:
  cantidad 18,4; costo y factor 18,6; monto 18,2; tarifa 9,6 como fracción;
  `LasCantidadesYCostosTienenSuPrecision`). Toda escritura con `Idempotency-Key`
  (`LosComandosDeInventarioLlevanClave`); todo trabajo de fondo por `IEjecutorEnCooperativa`
  (`NingunTrabajoDeFondoOperaSinCooperativa`); la segregación compara `SEC_Users.Id`, nunca el entero
  del token (`LaSegregacionNoUsaUserIdDelToken`); sin valores legales en el código
  (`ElComercioNoTieneValoresLegalesFijos`, `LaUvtSeLeeEnUnSoloSitio`). **En producción ninguna bodega
  se activa en I1**: sin la consulta de saldos de I2, `POST …/activation` responde
  `Inventory.Activation.AccountingUnavailable` (fuera de producción se activa aceptando la diferencia
  con permiso y motivo). Para las entregas que siguen quedan fijadas en el diseño —sus puertos todavía
  no existen en el código; sí las pruebas de arquitectura que los vigilan—: **la factura por el puerto
  `ICanalDeEmisionElectronica`** con modelo canónico propio y el proveedor tecnológico sólo en su
  adaptador (`ElProveedorTecnologicoSoloLoConoceSuAdaptador`, `LasCredencialesDeFacturacionNoTocanLaBase`),
  **crédito provisional hasta IC** (consultas a Cartera por `IConsultasDeCartera`, sin HTTP), y el
  número de tarjeta que no se guarda (`LosPagosNoGuardanElNumeroDeTarjeta`). La auditoría de los módulos
  encadenados (Inventory, ElectronicInvoicing, Integration, Approvals, Alerts, Parameters, Taxes,
  PaymentMeans, Navigation; **Accounting fuera**, C1) va por `COR_AuditOutbox` en la transacción y la
  sella y lleva a Mongo `AuditOutboxForwarder`, con anclas HMAC de una clave propia (`AnchorKeyVersion`,
  nunca `dev-v1`; la de desarrollo tiene su secreto en el repositorio); la verificación es manual
  (`POST /api/audit/integrity/verify`, pestaña «Integridad» de `/admin/auditoria`): **no hay verificación
  nocturna de la cadena**. Verde al cierre de I1: e2e «Inventario e2e» en PostgreSQL y SQL Server (398
  pasan y 6 omitidas con motivo en cada motor). Recetas: `docs/manual/inventario-documentos-y-kardex.md`,
  `docs/manual/plataforma-de-integracion.md`, `docs/manual/plantillas-de-importacion.md`; runbooks
  `docs/operaciones/inventario-retiro-heredado.md`, `docs/operaciones/inventario-puesta-en-marcha.md`,
  `docs/operaciones/auditoria-cadena-de-sellos.md`. Pendiente del dueño antes de promover: segundo
  revisor y respaldos por base (T985), usuario de Mongo sólo de inserción y clave de anclas propia en
  producción (T986), confirmar D8/D9/C10 (T987), QA en DEV/QA y MAUI a mano (T996, T997).

- **Cartera financiera (feature 013, rama `013-cartera-financiera`, K1 a K6b construidas (fases 1–23) y Polish local (fase 24,
  2026-10-17); rama local, sin merge ni despliegue)**: Cartera desde cero (créditos del sector solidario) en reemplazo de la heredada, por entregas K1–K6b; K1
  es el mínimo para salir en vivo y **no sale sin Recaudo y Tesorería** (en prueba, un recaudador y una tesorería de
  prueba, que no existen en producción). Hasta aquí: plataforma y retiro de las tablas `LND_` de crédito heredadas
  (fases 1–3), gobierno y seguridad (US17), líneas y usura (US1), originación (US2), desembolso, garantías y pagaré
  (US3), causación diaria y liquidación (US4), pagos por el **contrato de obligaciones** —Cartera no aplica pagos por
  pantalla— (US5), ventas a crédito de Comercial (US6) y la **integración contable** (US7, fase 11). Reglas que no se
  negocian de US7: **Cartera no conoce cuentas ni escribe asientos** —emite hechos `CAR → Accounting` en la misma
  transacción que su operación, ningún hecho lleva una cuenta, y Contabilidad sólo conoce a Cartera por
  `IDimensionesDeCartera` y la bandeja (`CarteraNoConoceContabilidad` en las dos direcciones,
  `LosHechosDeCarteraNoLlevanCuentas`)—; el destino `Accounting` sigue con **un solo consumidor**, `DestinoContabilidad`
  (mudado a `Application/Accounting/Integration` con lo común de los dos emisores), que es un **enrutador de dos
  emisores**: `INV` a los comandos de Inventario sin cambios, `CAR` a `PostLendingFactsCommand` /
  `PostLendingSummaryGroupCommand`, siempre por `AccountingPoster`; la **matriz** `ACC_LendingPostingRules` resuelve
  hecho × rol (26 roles) × dimensiones **por código** (exigidas por igualdad; opcionales con pesos Línea 512, Empleado
  256, Gracia 128, Sub-segmento 64, Reestructurado 32, PorCuentaDelAsociado 16, Sucursal 4 y Hecho 2 **sólo en los roles
  de resultado**; el hecho va en `*` en los roles de saldo, orden, pasivo, puente, diferido y contingentes; el centro de
  costo se asigna y no pesa, C77), con vigencia y sin retroactividad sobre lo contabilizado, plantilla con **filas
  sugeridas sin cuenta** (no se siembra ninguna regla) y completitud; **un comprobante por unidad** (una operación y su
  modo; los acompañantes sellan el del principal y el tipo sale del hecho principal: `DS`, `RC`, `DN` si recauda Nómina,
  `CA`, `PV` y el nuevo `CN` —24 tipos sembrados, 40 permisos contables—) y la causación **por lotes resumida**: un `CA`
  por día con la sucursal en cada línea y una línea por crédito donde la cuenta exige tercero o cruce; los reversos
  heredan el modo de su original; la **validación previa** (`IContabilidadParaCartera.EvaluarAsync`) la gobierna el
  parámetro `Contabilidad.FaltaDeRegla`, que **arranca en «Bloquear»** (A8, decisión del dueño del 2026-10-14): si falta
  la regla, la pantalla no confirma (`Lending.Prevalidation.NotPostable`), el contrato rechaza el pago, Nómina no
  aprueba (`Payroll.LoanDeductionNotPostable`), la venta de Comercial queda en reproceso y el proceso detiene su tanda y
  alerta (`Cartera.HechoNoContabilizable`); con «Advertir» todo se aplica, sella y alerta, y sin contabilidad iniciada o
  con el hecho en «no pasa» nada se bloquea. `.PoliticaSinRespuesta` (Contabilidad no responde) sigue bloqueando sólo
  en pantalla; lo de
  Cartera **no se reversa desde Comprobantes** (`Accounting.Document.ModuleOwned` con mensaje propio y
  `Accounting.Document.LendingCorrectsWithNewVoucher` en `PrepareReversalAsync`): se corrige con un **comprobante
  nuevo** de su propio hecho y fecha, con las dimensiones vigentes del saldo; Contabilidad **no cierra un mes que
  Cartera, activada, no cerró** (`Accounting.Period.LendingNotClosed`, sin reconocimiento; avisos reconocibles
  `LendingPending` y `LendingLoadWillExpire`), leyendo sólo recibos `ACC_LendingPostings` y entregas; la conciliación
  compara **por conjuntos de cuentas** y explica cada diferencia hecho por hecho; `DESC_CARTERA` vuelve a contabilizar
  en Nómina contra el puente «recaudos por nómina para Cartera» (enmienda D-08 de la 010). La 009 quedó enmendada en
  sus artefactos (E-01, 2026-10-13). **Las migraciones de K1 están congeladas desde el 2026-10-15 (T0802)**:
  `CarteraNucleo` (93 tablas `LND_`), `CarteraRiesgo` (7) e `IntegracionContableDeCartera` (cuatro `ACC_`), en ese orden
  detrás de `RetiroDeLaCarteraHeredada`, todas aditivas; todo cambio de esquema posterior (K2–K6b, la feature 014 de
  Ahorros o una corrección de K1) es una **migración nueva**, nunca una regeneración. **K2, libranza (fase 15,
2026-10-15)**: migración aditiva `CarteraLibranza` (8 tablas, 6 columnas nulas en tablas de K1, adopción por `CAR` de
`EnvioDeDescuentosConfirmado` y `ResultadoDeDescuentos`); convenios de pagaduría versionados con formatos de ida y vuelta
elegidos **por referencia** (ámbitos 5 y 6 de `COR_BankFileFormats`, genéricos, sin cruce de vigencias; `FlatFileReader` es
el inverso de `FlatFileWriter`), archivo de descuentos inmutable por pagaduría y período que va a Recaudo como
`DescuentosPorPagaduria`, y lo **descontado y no girado no causa mora** al deudor mientras la pagaduría esté dentro del
umbral `LIBRANZA_UMBRAL_MORA_PAGADURIA` (sin valor sembrado: sin él nunca se imputa); el giro se aplica por el contrato con
`DeductionFileLinePublicId` y fecha valor del descuento. Receta: `docs/manual/cartera-libranza-y-pagadurias.md`.
**K3 (fases 16–17, 2026-10-15)**: migración aditiva `CarteraNovedades` (9 tablas; mecanismo y causa en
`LND_AttributionRules`). Las novedades se clasifican por su **causa** (reestructuración o modificación,
`ClasificadorDeNovedad` sobre el `Anexo1`) y regeneran el plan; novación y refinanciación **trasladan** el saldo a un crédito
enlazado (`TransferToLinkedLoan`/`FromLinkedLoan`), no lo recogen por pago, y no giran dinero nuevo (decisión del dueño);
la excepción de cobertura la aprueba **otra persona**; el fracaso de la insolvencia mantiene el bloqueo (se levanta a mano)
y el acuerdo lo levanta solo. Castigo por **lote** que decide el consejo: al aplicar, el crédito que ya no cumple se
excluye y el resto se castiga; el castigado sigue causando en suspenso y concilia contra cuentas de orden. La evaluación
de cartera (comité y consejo) entra al cierre desde su mes de efecto y la última aplicada reemplaza a las anteriores.
Recetas: `docs/manual/cartera-novedades-y-condonaciones.md`, `docs/manual/cartera-castigo-y-evaluacion.md`.
**K4, cobranza (fase 18, 2026-10-16)**: migraciones aditivas `CarteraCobranza` (11 tablas) y `CarteraCobranzaSalida`
(`CollectorUserId` nulo en gestiones y asignaciones). Etapas versionadas por días de mora (semilla de cuatro «pendiente de
validar», Order 92), reparto por reglas, agenda, gestiones, promesas, acuerdos de pago, cobro jurídico, campañas, aviso
previo por episodio y reclamos ante centrales; el paso de cobranza es el paso 7 del ciclo diario (`CobranzaDelCicloDiario`).
Reglas: la **ventana de la Ley 2300 cuenta también las gestiones con contacto directo**; la cobranza nunca contacta
referencias; el ciclo **propone** el gasto y lo avisa (`GASTOCOBR`), y lo **causa una persona** sólo tras el aviso
`Sent`/`Delivered` (`Charge` por `RegistroDeCartera`); el tope del gasto es parámetro (`Cobranza.TopeGasto.Porcentaje` y
`.ValorMaximo`, sin semilla = sin tope); el crédito que se pone al día **sale de la cobranza** (historia `Source = Exit`,
gestión de sistema `LeftCollection` no anulable, asignación cerrada) y si recae es un episodio nuevo; el proceso jurídico
suspendido por insolvencia se reanuda **a mano** (`/resume`, nunca mientras la insolvencia negocia); la etapa migrada se
registra con una corrida (`process-runs` con `kind: MigratedCollectionStages` y motivo, o la tarea de una sola vez
`cartera.etapas-migradas-k4`), no con la semilla. SMS y WhatsApp reales siguen `SIMULADO` hasta que haya proveedor
(T0931/T0952). Recetas: `docs/manual/cartera-cobranza-y-aviso-previo.md`, `docs/operaciones/cartera-despliegue-k4.md`.
**K5, documentos, informes y regulatorios (fases 19–21, 2026-10-16)**: migraciones aditivas `CarteraInformes`
(`LND_RegulatoryFiles` y `LND_RegulatoryFileLines`) y `CarteraComprobanteDeCargue` (`ReceiptAttachmentPublicId` nulo).
Documentos al deudor: **extracto** de un período y **certificado tributario** del año (sólo créditos donde la persona es
deudora; el anticipo aplicado a intereses cuenta sólo si **ingresó** en el año; el saldo al 31/12 por conceptos según
`Documentos.CertificadoTributario.SaldoIncluye`, por defecto los cinco = el saldo de siempre, rige el vigente al emitir).
Informes `quality`, `vintages`, `concentration` (datos del deudor sólo agrupada por deudor o grupo), `issued-documents` y
`bureau-exclusions`, y el **tablero** `GET /api/lending/dashboard` (`/cartera/tablero`, sin datos personales); «vencida» es
más que `Informes.DiasCarteraVencida` (30, pendiente de validar). El **SICSES** (formato 9027, del cierre mensual vigente;
plazo por nivel de supervisión en `sicsesReport` de `TiposDeOrganizacion`, pendiente de validar) y los **archivos de
centrales** (novedades desde el último `Uploaded`; sólo con autorización `CreditBureauReport` con evidencia y, si el dato es
negativo, aviso previo `Complete` del titular —el codeudor recibe el suyo sólo con `AvisoPrevio.IncluirCodeudores`—; la mora
se reporta por **rangos configurables por modalidad**, `Centrales.RangosDeMora`, y es negativa desde el rango que diga; los
demás quedan excluidos con su motivo) salen de un motor genérico puro, `Domain/Common/Layouts` (la PILA no lo usa), sobre
layouts JSON embebidos con vigencia desde 2026-12-01 que **están sin cotejar** (`verified = false`): cada generación lleva
`Lending.Regulatory.LayoutSinCotejar` y la marca D28 va como advertencia, nunca dentro del archivo. Con bloqueantes queda una
versión `Validated` sin archivo (422 `BlockingIssues`); regenerar deja `Superseded`; marcar cargado con radicado y
comprobante cierra el período. Suscriptor por central en `Centrales.CodigosDeSuscriptor`. Rutas `/api/lending/regulatory`
(`RegulatoryFiles.Read`, nunca un `View`), pantallas `/cartera/sicses` y `/cartera/centrales`. Recetas:
`docs/manual/cartera-documentos-al-deudor.md`, `docs/manual/cartera-informes-y-tablero.md`,
`docs/operaciones/cartera-sicses-y-centrales.md`; cotejar los layouts es del dueño (T1011). **Las e2e de K5 están escritas
y se corren una sola vez al final de todas las fases** (decisión del dueño).
**K6a, pérdida esperada (fase 22, 2026-10-17)**: migración aditiva `CarteraPerdidaEsperada` (`LND_ImpairmentModelElections`,
`LND_ArrearsHistoryLoads` y siete columnas nulas de PE en `LND_MonthlyCloseLoans`, para el 9083) y `EntregaVigente = K6a`.
Consumo con y sin libranza y comercial de persona natural se deterioran por el **Anexo 2** (CE 68 de 2024) en las obligadas
desde su primer cierre y en las que lo adoptan: Z → puntaje logístico en `decimal` (`FuncionLogistica`, redondeo E9) → PI
de la tabla del tipo de organización (o `VOLUNTARIA`) × PDI por garantía (con varias, la mayor) × exposición, con la
calificación homologada (`MotorDePerdidaEsperada`). **No hay hechos contables nuevos**. Una obligada no activa sin el Anexo 2
ni el historial de mora completo (`RequirementsMissing`); los meses que faltan entran por la plantilla
`lending.arrears-history` y lo que siga faltando lo completa el cierre con la regla de meses faltantes y lo marca. La no
obligada adopta o desiste con acta, estudio y aviso subidos a la propuesta (pestaña «Modelo de deterioro» de
`/cartera/cierres`). El Anexo 2 sembrado va `verified = false` y **sin la PI voluntaria** (nadie adopta hasta registrarla);
el 9083 espera instructivo. Recetas: `docs/operaciones/cartera-perdida-esperada.md`, `docs/manual/cartera-perdida-esperada.md`.
**K6b, vivienda Ley 546 y UVR (fase 23, 2026-10-17)**: migración aditiva `CarteraVivienda` (15 columnas, ninguna tabla).
Con `EntregaVigente = K6b` se abre la modalidad Vivienda (reglas `LND-2026.2`): tasa fija bajo el menor entre
`TOPE_TASA_VIVIENDA` y la usura, sin mora sobre intereses, prepago sin penalidad; plazo, financiable general y VIS
(`IsSocialHousing`) y cuota-ingreso son cinco `LIMITE` sembrados «pendiente de validar»; el desembolso exige hipoteca de
primer grado (`MortgageRank`). Un crédito en **UVR** (sin tramos) se lleva en UVR a la diezmilésima y se valora en pesos con
la UVR de cada día, registrada **un valor por día** en la central (sin semilla); sin la UVR del día ese crédito no se causa
(`NationalValues.Missing`). La corrección monetaria diaria es el hecho masivo `CorreccionMonetariaUvrCausada` (CMU,
comprobante `CA`; propuesta por defecto de E12). Las respuestas ganan `housing`, `uvr`, `denominationUnit` e
`isSocialHousing` opcionales (api.md §29). Recetas: `docs/manual/cartera-vivienda-y-uvr.md`,
`docs/operaciones/valores-nacionales.md` §3a.
**Reglas transversales de la 013 (cierre local, 2026-10-17)**: **un solo escritor del libro de Cartera**,
`RegistroDeCartera` (`NadieEscribeElLibroDeCarteraFueraDelRegistro`); **una sola fuente de saldos**,
`ReconstruccionDeCredito` (no hay saldos guardados que no salgan de ella); los pagos entran sólo por el **contrato de
obligaciones v1**; los **valores nacionales** (usura, UVR, topes, tipos de organización, Anexo 2) se registran una vez en la
base administrativa (`ADM_NationalValues`, `ADM_RegulatoryDocuments`) y se **replican** a `COR_` de cada cooperativa; corregir
una USURA o una UVR ya replicada encola la corrida `NationalValueCorrection`, que corrige desde el día afectado hasta la última
marca con filas nuevas (nunca edita lo aplicado) y manda el reparto en pesos de un pago de ese día en su propio hecho
`PagoRedistribuidoPorValorNacional` (PRN, comprobante `CA`; decisión del dueño); los **cierres son inmutables**; el modelo de
deterioro va por cooperativa y segmento; **los layouts son dato** con cotejo (`verified`); los documentos al deudor llevan
código de verificación; los permisos `Lending.*` los siembra la API al arrancar; las pruebas de DT §2.25 tienen nombre fijo.
Verde al cierre local: e2e en PostgreSQL y SQL Server (Lending 108 pasan y 7 omitidas de rendimiento; todo el proyecto 585 y
16). Índice de recetas: `docs/manual/cartera.md`. Pendiente del dueño o de ambiente: cotejar layouts y Anexo 2, PI voluntaria,
UVR y topes reales en la central, T1078–T1089 (QA, MAUI, despliegue).
**Decisiones del dueño del 2026-10-18 (T1091–T1097, sin migración)**: el reparto de un pago se corrige con
`PagoRedistribuidoPorValorNacional` (PRN) también en la corrida de una **usura** (el saldo a favor que deja un pago viaja
ahí; mismo asiento neto que C93), y las `reallocations` de `PagoAplicado`/`PagoReversado` se asientan **en el comprobante
del pago o del reverso** con el constructor de PRN (`contabilidad.md` §30–§31). En las centrales, la mora bajo el rango
negativo de `Centrales.RangosDeMora` va **con sus días reales y sin marca negativa**. Plazos y periodicidades regulatorias
son **informativos**: nada se bloquea por ellos; la evaluación de cartera fuera de periodicidad avisa
(`PortfolioEvaluationOutsidePeriodicity`); cada cooperativa fija su periodicidad del SICSES con
`Sicses.MesesDeCorte`/`Sicses.DiasDePlazo`, y el último día hábil descuenta los `FESTIVO` (`ICalendarioHabil`). El
certificado tributario lleva en renglones aparte el **anticipo recibido** en el año y pendiente al 31/12 y la **corrección
monetaria UVR** del año. Los **requisitos de la activación se exigen en todos los ambientes** (no hay `Waived`); fuera de
producción sólo cuentan como recaudador y tesorería los dobles `TST`/`TES`, y las e2e los cumplen con
`CarteraE2E.RequisitosDeActivacionAsync`. Confirmado: SOLIDO sin créditos en UVR, un solo tope de vivienda, UVR sin
tramos, avisar al cargar y bloquear al activar el historial faltante (`78926dd2`); el pago que cambia de cancelar por la
UVR sigue a soporte.
Rutas `/api/accounting/lending/*`, `/api/accounting/regulatory-indicators`,
  `/api/lending/accounting/*` y `/api/lending/messages`; pantallas `/contabilidad/cartera/{matriz, tipos-de-comprobante,
  completitud, lotes, lotes/{id}}`, `/contabilidad/indicadores-regulatorios`, `/cartera/conciliacion-contable`, la
  pestaña «Emitidos» de `/cartera/bandeja-de-mensajes` y los reconocimientos de Cartera en `/contabilidad/periodos`.
  Recetas: `docs/manual/contabilidad-contrato-de-contabilizacion.md` §9 y `docs/manual/cartera-integracion-contable.md`
  (para la contadora, con lo pendiente de validar); las de fases anteriores, `docs/manual/cartera-*.md`.

- **Captaciones y aportes (feature 014, rama `014-ahorros-aportes-cdt`, K7 a K11 y Polish, 2026-10-19; rama local, sin
  merge ni despliegue)**: ahorro a la vista, permanente, contractual, CDAT y aportes sociales **dentro de la Cartera de la
  013**, en reemplazo de las captaciones heredadas, retiradas con `RetiroDeCaptacionesHeredadas` (destructiva con guarda;
  el dueño autorizó y es el segundo revisor, D15) junto con `/api/savings` y sus pantallas. **La línea es común**
  (`LND_CreditLines.Nature`: colocación, captación, aportes) con versión de condiciones propia por naturaleza, y la cuenta
  es una sola entidad, `MemberAccount`. Reglas que no se negocian: **un solo escritor del libro de cuentas**,
  `RegistroDeCuentas`, y **una sola fuente de saldos**, `ReconstruccionDeCuenta` (a la fecha D con lo conocido hasta R);
  **los movimientos de caja entran sólo por el contrato de cuentas** (`IContratoDeCuentas` v1: consignar, retirar,
  trasladar, reversar), como los pagos por el contrato de obligaciones —la cancelación en efectivo por el contrato sigue
  rechazada hasta que exista Recaudo—; **Captaciones no conoce cuentas**: emite hechos por la misma matriz
  `ACC_LendingPostingRules` y `DestinoContabilidad` (17 roles, 5 dimensiones nuevas que no pesan, comprobantes `MA`, `CC`,
  `TD`, `AO`, `RA`) con la validación previa de `Contabilidad.FaltaDeRegla`; el ciclo diario corre por
  `PasoDeCaptaciones`/`ISubpasoDeCaptaciones` y `CicloDelCdat`; lo que sale de la transacción (el correo de una reversión
  tardía) va por `TareasTrasElCommit`; **sin valores legales en el código** (componente inflacionario, inembargable, tope
  del seguro de depósitos y tope de entrega sin sucesión son valores nacionales; lo demás, parámetros con vigencia);
  motores puros con **67 casos dorados** (`Domain.Tests/Lending/Deposits/Casos`, recalculados con `calcular.py`);
  requisitos de activación en **todos** los ambientes (D14), activación por naturaleza y roles sugeridos `DeCaptaciones`
  (D18). Entregas: **K7** aportes, cuota, recaudo por Nómina y pagadurías, retiro del asociado con cruce de deuda; **K8**
  ahorro a la vista, intereses, retención con componente inflacionario, GMF, cargos, débito automático, bloqueos,
  embargos con límite inembargable, documentos al titular; **K9** CDAT (título no negociable, tasa negociada) y ahorro
  contractual; **K10** revalorización, excedentes y amortización de asamblea, inactividad, no reclamados y fallecimiento;
  **K11** fondo de liquidez (Decreto 976 de 2026), IRL y brecha, depósitos asegurados, SICSES 9026, 9068, 9013, 169 y
  9067 y exógena 1019 y 1020, **sin cotejar** (`verified = false`; plazos informativos, como en la 013).
  **`EntregaVigente = K11`**: las cinco salen juntas y ninguna va a producción sin Recaudo y Tesorería reales.
  **Migraciones**: una sola par, `CaptacionesAportes` (53 tablas `LND_`, SQL a mano para T044, `NOM` y `ASO`),
  **congelada desde el 2026-10-19 (T225)** con `RetiroDeCaptacionesHeredadas` y las de la 013:
  `LasMigracionesCongeladasNoSeRegeneran` guarda la huella de cada `.cs` y `.Designer.cs` (comentarios incluidos: anotar
  respaldo y revisor en la cabecera exige actualizar la huella en el mismo commit); todo cambio de esquema es una
  migración nueva. 180 rutas nuevas bajo `/api/lending/...` (`Endpoints/Lending/Deposits/`), permisos `Lending.*` (163,
  los siembra la API); pantallas `/cartera/captaciones/*`, `/cartera/fondo-de-liquidez`,
  `/cartera/captaciones/reportes-regulatorios`; la ficha de la persona muestra sólo lo del titular y las cuentas como
  cotitular salen aparte (`GET /api/core/people/{id}/co-held-accounts`). Recetas: `docs/manual/cartera-captaciones.md`
  (índice y ocho temáticos), `contabilidad-contrato-de-contabilizacion.md` §10, runbooks
  `docs/operaciones/captaciones-puesta-en-marcha.md`, `captaciones-despliegue.md` y `captaciones-retiro-heredado.md`.
  Pendiente del dueño: medir el piloto (T234), validar con la contadora la matriz y sus cuentas CUIF, los casos dorados y
  los siete layouts (T235), promover (T236), MAUI a mano y cargar los valores nacionales sin semilla; preguntas abiertas
  en `specs/014-ahorros-aportes-cdt/research.md`.

## Arquitectura
Clean Architecture en 4 capas:
- `src/Core/` — Domain, Application
- `src/Infrastructure/` — EF Core, repositorios, servicios externos
- `src/Presentation/` — APIs Carter, Blazor Web, Blazor MAUI

## Totales

Instantánea del 2026-10-29 (develop con la 015 y la 017), remedida. **Son cifras que
envejecen**: las de antes llevaban meses desfasadas —decían 113 endpoints cuando había
~619, y 398 pruebas cuando eran 616— y nadie lo notaba porque nada las contrasta. Si
dudás, medí en vez de creerles; el comando está al lado.

| | | cómo medirlo |
|---|---|---|
| Rutas REST | 1.733 (2026-10-29, develop con la 015 —Recaudo y Tesorería— y la 017 —identidad con documento—). Antes: 1.403 (2026-10-19, feature 014: +180 de Captaciones y aportes, −74 del ahorro heredado y otras retiradas). Antes: 1.322 (2026-10-17, feature 013: +466 sobre las 856 de I1 —Cartera K1 a K6b, su integración contable, valores nacionales y regulatorios—). Antes: 856 (2026-09-26, I1 de la feature 012: +84 sobre las 772 del cierre de la 011 —catálogo, bodegas, documentos, compras, conteos, traslados, períodos, puesta en marcha, aprobaciones, parámetros, alertas, alcances, informes de inventario, impuestos—, ya descontadas las del inventario heredado que se retiraron) | `grep -rhE "^\s*[a-zA-Z]+\.Map(Get\|Post\|Put\|Delete\|Patch)\(" --include=*.cs src/Presentation/IngenIA365ERP.API/Endpoints/ \| wc -l` |
| Páginas Blazor | 352 con `@page` (2026-10-29, develop con la 015 y la 017). Antes: 281 con `@page` (2026-10-19, feature 014: retiró las pantallas heredadas de ahorro y sumó `/cartera/captaciones/*` y `/cartera/fondo-de-liquidez`). Antes: 283 (2026-10-17, feature 013: +82, `/cartera/*` y las de Contabilidad para Cartera). Antes: 201 (2026-09-26; I1 de la 012 retiró las 21 pantallas del inventario heredado y sumó las suyas: `/inventario/*`, `/compras/*`, `/ventas/canales`, `/ventas/vendedores`, `/maestros/impuestos`) | `grep -rl "@page" --include=*.razor src/Presentation/IngenIA365ERP.Shared/Pages/ \| wc -l` |
| Reportes PDF | 18 clases `*Report` (2026-10-29, develop con la 015 y la 017). Antes: 16 clases `*Report` (2026-10-19, feature 014: se retiraron `PersonPortfolioReport`, `CdtCertificateReport` y `SavingsStatementReport` y se sumó `DocumentosDeCaptacionesReport`, en la subcarpeta `Reports/Captaciones/`: por eso el comando ahora es recursivo). Antes: 18 (2026-10-17, feature 013: +6, documentos al deudor y de Cartera) | `grep -rhoE "static class [A-Za-z]+Report\b" --include=*.cs src/Presentation/IngenIA365ERP.API/Reports/ \| wc -l` |
| Pruebas sin contenedores | 11.375 el 2026-10-29, cierre de la 017 sobre develop con la 015 (2.842 Domain, 7.092 Application, 921 Architecture, 520 Shared), todas pasan. Antes: 10.078 el 2026-10-19, cierre local de la 014 (2.630 Domain, 6.193 Application, 757 Architecture, 498 Shared), todas pasan. Antes: 9.109 el 2026-10-17, cierre local de la 013 (2.298 Domain, 5.706 Application, 638 Architecture, 467 Shared), todas pasan. Antes: 3.215 el 2026-09-26 (516 Domain, 2.324 Application, 209 Architecture, 164 Shared, 2 Load) | `dotnet test tests/IngenIA365ERP.<X>.Tests` |
| Pruebas de integración | 753 el 2026-10-29 en PostgreSQL **y** SQL Server (cierre de la 017 sobre develop con la 015): 718 pasan y 20 omitidas con su motivo en cada motor; las 15 de adjuntos sobre S3 fallan porque `quay.io/minio` responde 401 al bajar la imagen (ambiente, no código). Antes: 601 el 2026-10-17 en PostgreSQL **y** SQL Server: 585 pasan y 16 omitidas con su motivo en cada motor (Lending 108 y 7, las de rendimiento que piden `RUN_PERF_TESTS=1`); filtro por módulo `--filter "FullyQualifiedName~IntegrationTests.Lending"`, motor por `DB_PROVIDER`. Antes: 404 el 2026-09-26 con Docker en PostgreSQL: 398 pasan y 6 omitidas con su motivo (la de hashes, `[Fact(Skip)]`, y cinco de la 012: tres de volumen que sólo corren con `RUN_PERF_TESTS=1`, una que espera los tipos fiscales de I3/I4 y una que por HTTP no puede fechar la foto de un conteo en un mes ya terminado); una cayó por un contenedor de Mongo que no arrancó (código 48) y pasó al repetirla. Las colecciones «Nomina e2e», «Contabilidad e2e» e «Inventario e2e» corren en paralelo sobre contenedores distintos; las de adjuntos suman MinIO de `quay.io` y el host «Adjuntos sobre S3» (`ApiConAlmacenS3Fixture`) | `dotnet test tests/IngenIA365ERP.API.IntegrationTests` |
| Errores de compilación | 0 | `dotnet build IngenIA365ERP.slnx` |

**Las de integración** levantan contenedores (Testcontainers) y exigen Docker Desktop
corriendo, y **nada más**: desde el 2026-09-23 la fixture pone su propio WebAuthn, su maestro
(`MasterAdmin:*` con sus constantes de prueba) y un par de llaves JWT generado por corrida. Hasta esa
fecha dependía de tres cosas que git ignora —`appsettings.Development.json`,
`appsettings.Development.local.json` y `Keys/`— y en un clon limpio o un worktree todas caían en
`ObjectDisposedException`. Las 13 de nómina (`Payroll/`, colección «Nomina e2e», una cooperativa
compartida) recorren por HTTP el ciclo entero contra PostgreSQL, Mongo y Redis reales, y
`PayrollCyclePerformanceTests` sólo mide con `RUN_PERF_TESTS=1`. **Hay una sola fixture**,
`CentralIdentityApiFixture` (contenedor por proveedor según `DB_PROVIDER`, migraciones EF,
maestro con segundo factor, cooperativas por `/api/saas/tenants/with-admin`); las clases
que sólo comprueban la puerta comparten host por la colección «Identidad central
compartida». `ApiTestFixture`, la de Fase 0 (SQL Server fijo, cooperativa «demo» sobre
`dbo`), se retiró el 2026-09-06: sus 18 pruebas llevaban desde el 2026-08-24 cayendo en
1 ms porque el store multi-tenant (`ErpTenantInfo`) insertaba `ProvisioningState` en NULL
explícito sobre una columna `NOT NULL DEFAULT 'Pending'`; el default en el mapeo lo cerró
y `DosContextosUnaTablaTests` lo vigila.

La única omitida es `PasswordHashIntegrityTests.AllHashes_must_meet_cost_threshold`,
marcada `[Fact(Skip)]`. Pero **el verde tapa siete métodos más** que hacen `return`
al principio según una variable de entorno y se cuentan como **pasados** sin haber
comprobado nada: `AuditPerformanceTests` (`RUN_PERF_TESTS`), `LoginThroughputFact`
(`RUN_LOAD_TESTS`), cuatro de multi-tenancy (`ERP_TEST_PG`) y
`PrincipioVI_PublicIdOnly`. Es una forma de no correr que no aparece en el resumen.
Eran ocho: `AuditExportPdfSignatureTests` pegaba **anónima**, recibía 401 y lo
afirmaba como el estado esperado, de modo que la verificación HMAC de la
exportación no se había ejecutado nunca. Ya corre de verdad, y con la mitad que le
faltaba —un PDF con un byte cambiado tiene que dar `valid: false`—.
- Sistema de diseño en `src/Presentation/IngenIA365ERP.Shared/wwwroot/css/`:
  - `tokens.css` — única fuente de color, densidad, escala y contraste
  - `componentes.css` — clases de pantalla (`.pagina`, `.page-header`, `.toolbar`, `.info-card`, `.kpi-card`, `.data-grid`…)
  - Ninguna pantalla declara colores literales ni bloques `<style>` propios
  - **Indicador de carga por zona** (desde el 2026-09-13): toda grilla, formulario o panel que
    espera datos va dentro de `<IndicadorDeCarga Cargando="@_carga.Activa">` con una
    `EstadoDeCarga` por zona (`using var carga = _carga.Iniciar();` en el método que carga;
    se apaga sola). **Obligatorio en toda pantalla nueva o modificada** que cargue o guarde
    datos; está en todo Core y Nómina, y la prueba de arquitectura
    `LasPantallasDicenQueEstanCargando` lo exige en los módulos migrados (se amplía la lista
    `ModulosMigrados` al migrar otro). Receta: `docs/manual/indicador-de-carga.md`. El velo
    global (`ILoadingService`) no se usa para esto.

## Cómo Empezar
Ver `README.md` para instrucciones de ejecución y `docs/INDICE-DOCUMENTACION.md` para la documentación completa por fase.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
[specs/017-login-documento-invitacion/plan.md](specs/017-login-documento-invitacion/plan.md)
along with its companion artifacts:
- [spec.md](specs/017-login-documento-invitacion/spec.md)
- [analisis-sistema-actual.md](specs/017-login-documento-invitacion/analisis-sistema-actual.md)
- [diagnostico-de-datos.md](specs/017-login-documento-invitacion/diagnostico-de-datos.md)
- [research.md](specs/017-login-documento-invitacion/research.md)
- [data-model.md](specs/017-login-documento-invitacion/data-model.md)
- [quickstart.md](specs/017-login-documento-invitacion/quickstart.md)
- [contracts/](specs/017-login-documento-invitacion/contracts/)
<!-- SPECKIT END -->
