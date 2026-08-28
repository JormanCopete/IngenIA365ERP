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
- **Cifrado**: el llavero de DataProtection vive en `ADM_DataProtectionKeys`, no
  en el proceso. Cifra los secretos TOTP **y la clave de cada adjunto**: antes de
  desplegarlo hay que rescatar las claves de cada pod, o los archivos cifrados
  quedan ilegibles. Ver `docs/operaciones/llavero-dataprotection.md`.
- **Multi-tenancy**: Una base de datos por cooperativa (constitución v2.0.0, Principio IV).
  El aislamiento es físico. La base administrativa `IngenIA365ERP_Admin` es una sola y
  vive fuera de toda base de cooperativa.
- **Reportes**: QuestPDF (16 reportes)

## Arquitectura
Clean Architecture en 4 capas:
- `src/Core/` — Domain, Application
- `src/Infrastructure/` — EF Core, repositorios, servicios externos
- `src/Presentation/` — APIs Carter, Blazor Web, Blazor MAUI

## Totales

Instantánea del 2026-08-26, remedida. **Son cifras que envejecen**: las de antes
llevaban meses desfasadas —decían 113 endpoints cuando había ~619, y 398 pruebas
cuando eran 616— y nadie lo notaba porque nada las contrasta. Si dudás, medí en
vez de creerles; el comando está al lado.

| | | cómo medirlo |
|---|---|---|
| Rutas REST | ~626 en 137 archivos | `grep -rhE "^\s*[a-zA-Z]+\.Map(Get\|Post\|Put\|Delete\|Patch)\(" --include=*.cs src/Presentation/IngenIA365ERP.API/Endpoints/ \| wc -l` |
| Páginas Blazor | 175 con `@page` | `grep -rl "@page" --include=*.razor src/Presentation/IngenIA365ERP.Shared/Pages/ \| wc -l` |
| Reportes PDF | 16 | |
| Pruebas | 648 (647 pasan, 1 con `RUN_PERF_TESTS=1`) | `dotnet test IngenIA365ERP.slnx` |
| Errores de compilación | 0 | `dotnet build IngenIA365ERP.slnx` |
- Sistema de diseño en `src/Presentation/IngenIA365ERP.Shared/wwwroot/css/`:
  - `tokens.css` — única fuente de color, densidad, escala y contraste
  - `componentes.css` — clases de pantalla (`.pagina`, `.page-header`, `.toolbar`, `.info-card`, `.kpi-card`, `.data-grid`…)
  - Ninguna pantalla declara colores literales ni bloques `<style>` propios

## Cómo Empezar
Ver `README.md` para instrucciones de ejecución y `docs/INDICE-DOCUMENTACION.md` para la documentación completa por fase.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan at
[specs/004-multi-motor-bd/plan.md](specs/004-multi-motor-bd/plan.md)
along with its companion artifacts:
- [spec.md](specs/004-multi-motor-bd/spec.md)
- [research.md](specs/004-multi-motor-bd/research.md)
- [data-model.md](specs/004-multi-motor-bd/data-model.md)
- [quickstart.md](specs/004-multi-motor-bd/quickstart.md)
- [contracts/](specs/004-multi-motor-bd/contracts/)
<!-- SPECKIT END -->
