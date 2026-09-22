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
  vive fuera de toda base de cooperativa.
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
  aceptan (una oficina, una sola sucursal: `Branch.OfficeAlreadyLinked`); la pantalla de Agencias
  todavía no lo ofrece.
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

## Arquitectura
Clean Architecture en 4 capas:
- `src/Core/` — Domain, Application
- `src/Infrastructure/` — EF Core, repositorios, servicios externos
- `src/Presentation/` — APIs Carter, Blazor Web, Blazor MAUI

## Totales

Instantánea del 2026-09-21 (cierre de N2 de la feature 010, en su rama), remedida. **Son cifras que
envejecen**: las de antes llevaban meses desfasadas —decían 113 endpoints cuando había
~619, y 398 pruebas cuando eran 616— y nadie lo notaba porque nada las contrasta. Si
dudás, medí en vez de creerles; el comando está al lado.

| | | cómo medirlo |
|---|---|---|
| Rutas REST | 765 (2026-09-22; +2 de la carga masiva de auxiliares sobre las 763 de E2) | `grep -rhE "^\s*[a-zA-Z]+\.Map(Get\|Post\|Put\|Delete\|Patch)\(" --include=*.cs src/Presentation/IngenIA365ERP.API/Endpoints/ \| wc -l` |
| Páginas Blazor | 182 con `@page` (2026-09-21; E2 contable sumó libro auxiliar, informes, estados financieros, tercero, presupuesto y `/contabilidad/apertura`) | `grep -rl "@page" --include=*.razor src/Presentation/IngenIA365ERP.Shared/Pages/ \| wc -l` |
| Reportes PDF | 13 clases `*Report` (2026-09-21; `SettlementDocumentReport` para la firma de la definitiva) | `grep -rhoE "static class [A-Za-z]+Report\b" src/Presentation/IngenIA365ERP.API/Reports/*.cs \| wc -l` |
| Pruebas sin contenedores | 1.664 el 2026-09-22 (223 Domain, 1.225 Application, 113 Architecture, 101 Shared, 2 Load), todas pasan | `dotnet test tests/IngenIA365ERP.<X>.Tests` |
| Pruebas de integración | 213 el 2026-09-22 con Docker: 212 pasan, 1 omitida (colecciones «Nomina e2e» y «Contabilidad e2e» en paralelo sobre contenedores distintos) | `dotnet test tests/IngenIA365ERP.API.IntegrationTests` |
| Errores de compilación | 0 | `dotnet build IngenIA365ERP.slnx` |

**Las de integración** levantan contenedores (Testcontainers) y exigen Docker Desktop
corriendo. Las 13 de nómina (`Payroll/`, colección «Nomina e2e», una cooperativa
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
[specs/010-nomina-prestaciones-pila-dian/plan.md](specs/010-nomina-prestaciones-pila-dian/plan.md)
along with its companion artifacts:
- [spec.md](specs/010-nomina-prestaciones-pila-dian/spec.md)
- [research.md](specs/010-nomina-prestaciones-pila-dian/research.md)
- [data-model.md](specs/010-nomina-prestaciones-pila-dian/data-model.md)
- [quickstart.md](specs/010-nomina-prestaciones-pila-dian/quickstart.md)
- [contracts/](specs/010-nomina-prestaciones-pila-dian/contracts/)
<!-- SPECKIT END -->
