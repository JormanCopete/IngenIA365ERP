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
- **Frontend**: Blazor Hybrid MAUI + Web + WebAssembly, SyncFusion 33.1.44
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
  `appsettings.json` no las leía nadie y se retiraron.
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
  sin semilla). El **cálculo preliminar** anterior
  (`POST /api/payroll/process`, salud y pensión fijas al 4 %) se retiró sin alias.

## Arquitectura
Clean Architecture en 4 capas:
- `src/Core/` — Domain, Application
- `src/Infrastructure/` — EF Core, repositorios, servicios externos
- `src/Presentation/` — APIs Carter, Blazor Web, Blazor MAUI

## Totales

Instantánea del 2026-09-05 (cierre de la feature 005), remedida. **Son cifras que
envejecen**: las de antes llevaban meses desfasadas —decían 113 endpoints cuando había
~619, y 398 pruebas cuando eran 616— y nadie lo notaba porque nada las contrasta. Si
dudás, medí en vez de creerles; el comando está al lado.

| | | cómo medirlo |
|---|---|---|
| Rutas REST | 674 en 142 archivos | `grep -rhE "^\s*[a-zA-Z]+\.Map(Get\|Post\|Put\|Delete\|Patch)\(" --include=*.cs src/Presentation/IngenIA365ERP.API/Endpoints/ \| wc -l` |
| Páginas Blazor | 180 con `@page` | `grep -rl "@page" --include=*.razor src/Presentation/IngenIA365ERP.Shared/Pages/ \| wc -l` |
| Reportes PDF | 15 clases `*Report` | `grep -rhoE "static class [A-Za-z]+Report\b" src/Presentation/IngenIA365ERP.API/Reports/*.cs \| wc -l` |
| Pruebas sin contenedores | 781 (136 Domain, 564 Application, 54 Architecture, 25 Shared, 2 Load), todas pasan | `dotnet test tests/IngenIA365ERP.<X>.Tests` |
| Pruebas de integración | 134 el 2026-09-10 con Docker: 133 pasan, 1 omitida | `dotnet test tests/IngenIA365ERP.API.IntegrationTests` |
| Errores de compilación | 0 | `dotnet build IngenIA365ERP.slnx` |

**Las de integración** levantan contenedores (Testcontainers) y exigen Docker Desktop
corriendo. Las 10 de nómina (`Payroll/`, colección «Nomina e2e», una cooperativa
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

## Cómo Empezar
Ver `README.md` para instrucciones de ejecución y `docs/INDICE-DOCUMENTACION.md` para la documentación completa por fase.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
[specs/005-nomina-novedades-liquidacion/plan.md](specs/005-nomina-novedades-liquidacion/plan.md)
along with its companion artifacts:
- [spec.md](specs/005-nomina-novedades-liquidacion/spec.md)
- [research.md](specs/005-nomina-novedades-liquidacion/research.md)
- [data-model.md](specs/005-nomina-novedades-liquidacion/data-model.md)
- [quickstart.md](specs/005-nomina-novedades-liquidacion/quickstart.md)
- [contracts/](specs/005-nomina-novedades-liquidacion/contracts/)
<!-- SPECKIT END -->
