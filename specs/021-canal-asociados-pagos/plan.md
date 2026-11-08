# Implementation Plan: Canal digital del asociado y pagos en línea

**Branch**: `021-canal-asociados-pagos` | **Date**: 2026-11-08 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/021-canal-asociados-pagos/spec.md` (decisiones del dueño D1–D16 y
aclaraciones del 2026-11-08).

## Summary

Un **sistema satélite** nuevo —servicio Go con base propia, interfaz React como PWA y envoltura MAUI HybridWebView
para las tiendas— le da al asociado ingreso con documento y clave, consulta de sus conceptos en cada cooperativa y
pago por la pasarela de la cooperativa (Avalpay/Placetopay primero). El satélite **nunca** toca una base del ERP: lee
y escribe por una **API de servicio** nueva del ERP (`/api/canal/v1`), autenticada con tokens de servicio ES256 y
resuelta por cooperativa.

El pago aprobado entra al ERP como **recibo de canal en línea** de Recaudo, contra el convenio de pasarela, por la
misma `AplicacionNoInteractiva` que ya aplica los archivos de pasarela de la 015, a nombre del asociado (enmienda
acotada de FR-174 y FR-091 de la 015). Así el pago se aplica por el contrato de obligaciones o el de cuentas, con su
prelación, su asiento y su recibo. La conciliación diaria es el flujo de archivos de la 015, alineado por la misma
llave idempotente. El ERP gana un módulo **Canal digital** (`CDG_*`) con configuración, conceptos, destinos,
habilitaciones, novedades de contacto y avisos.

Requiere antes la **enmienda de la constitución** (v2.1.0, [enmienda-constitucion.md](enmienda-constitucion.md)).

Decisiones en [research.md](research.md) (R1–R19); esquema en [data-model.md](data-model.md); contratos en
[contracts/](contracts/); validación en [quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: ERP: C# / .NET 10.0.5 (lo existente). Canal: Go 1.26+ (`canal/api`), TypeScript + React 19
(`canal/web`), C# / .NET 10 MAUI (`canal/app`).

**Primary Dependencies**:
- ERP: lo existente; **ninguna nueva** (el JWT ES256 lo valida la misma librería JwtBearer).
- Canal Go: `pgx/v5`, `sqlc`, `goose`, River, `go-webauthn/webauthn`, `golang.org/x/crypto`,
  `golang-jwt/jwt/v5`, cliente FCM, `testcontainers-go`.
- Canal web: Vite, React Router, TanStack Query, react-hook-form, zod, vite-plugin-pwa, Vitest, Playwright.
- App: `Microsoft.Maui.Controls` 10, `Plugin.Firebase`.

**Storage**:
- ERP, base de cada cooperativa: 8 tablas `CDG_*` nuevas, un índice único en `RCD_GatewayFileLines` y, en C6,
  columnas en `CreditApplications`. Todas son migraciones aditivas por proveedor (PostgreSQL y SQL Server).
- Canal: PostgreSQL `ingenia365_canal`, una por ambiente, con migraciones `goose`.
- Redis base 0: almacén de `jti` de los tokens de servicio.

**Testing**: ERP xUnit (Domain, Application, Architecture, Shared) e integración con Testcontainers al final. Canal
`go test` con testcontainers y pasarela simulada; Vitest; Playwright para el flujo de pago; pruebas de contrato en
los dos lados.

**Target Platform**: k3s (DEV, QA, PDN) detrás de Cloudflare Tunnel y Traefik. Navegadores móviles y de escritorio
(PWA). Android 7+ (API 24) e iOS 15+.

**Project Type**: web service (Go) + SPA/PWA + app móvil híbrida + módulo del ERP (web service .NET + Blazor).

**Performance Goals**: consulta de conceptos p95 < 3 s (SC-009); 99 % de los pagos aplicados < 5 min desde la
aprobación (SC-003); ingreso < 1 s sin contar el código.

**Constraints**:
- Ningún saldo guardado en el canal.
- Ningún dato de tarjeta en ningún sistema.
- Secreto de la pasarela sólo cifrado en el canal.
- Sesión de 5 min de inactividad y 8 h máximo.
- Respuesta idéntica exista o no la persona.
- Un solo dominio para todas las cooperativas.

**Scale/Scope**: primer año, ~15 cooperativas, ~50 000 cuentas, ~20 000 pagos/mes, picos de fin de mes de ~200
pagos/min. Seis entregas (C1–C6) más C0.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Dos lecturas: el **ERP** (módulo Canal digital y API de servicio) cumple los doce principios sin excepción; el
**satélite** cumple las reglas de la sección «Sistemas satélite» que propone la enmienda C0.

| #    | Principio | Estado | Nota |
|------|-----------|--------|------|
| I    | Spec-First | PASS | Spec + aclaraciones + este plan; la enmienda C0 se aprueba antes de codificar. |
| II   | Clean Architecture | PASS (ERP) / **Satélite** | ERP: entidades en Domain, comandos en Application, endpoints Carter en API; el satélite es otro sistema, fuera de las capas (enmienda). |
| III  | CQRS + MediatR | PASS (ERP) | Toda ruta de `/api/canal/v1` y `/api/channel` reenvía a un comando o consulta con validador. El satélite no usa MediatR (enmienda). |
| IV   | Multi-tenancy | PASS | La API de servicio resuelve la cooperativa de la ruta + token y llena el mismo contexto que el middleware; el listado de cooperativas recorre el directorio; el satélite nunca abre una base de cooperativa. El espejo de habilitaciones lleva la cooperativa en cada fila y nunca cruza datos. |
| V    | Person centralizada | PASS | `CDG_ChannelEnablements` y `CDG_ContactUpdates` apuntan a `COR_People`; la novedad de contacto no duplica ni sobrescribe la persona. |
| VI   | PublicId | PASS | Sólo `PublicId` viaja al canal; el canal usa UUID propios. |
| VII  | Soft-delete + auditoría | PASS | `CDG_*` heredan `AuditableEntity` con filtro; en el satélite, estados en lugar de DELETE (enmienda). |
| VIII | Validación dual | PASS | Validadores FluentValidation en el ERP; zod en React + validación en Go del lado del canal. |
| IX   | Errores visibles | PASS | Sobre de error con código; `slog` estructurado en el canal; el aviso fallido se reintenta y alerta, nunca se descarta. |
| X    | Trazabilidad | PASS | El aviso pasa por `AuditBehavior` con actor asociado, IP y agente; lecturas sensibles auditadas; auditoría del canal sólo de anexar, ≥ 5 años. |
| XI   | Inmutabilidad contable | PASS | El pago se reversa con `ReversarAsync` (asiento espejo); `CDG_PaymentNotices` no se edita salvo el bloque de reverso. |
| XII  | Migraciones | PASS | Migraciones EF aditivas por proveedor; `goose` con Up/Down en el canal; la única destructiva (retiro de `WEB_*`) queda fuera. |
| UI   | `IndicadorDeCarga` | PASS | Las seis pantallas de `/canal/*` lo llevan; se agrega «Canal» a `ModulosMigrados`. La PWA tiene su propio indicador por zona (mismo criterio). |
| Satélite | Reglas 1–9 de la enmienda | PASS condicionado | Condicionado a aprobar C0. |

**Re-chequeo tras el diseño**: sin violaciones nuevas.
- Las dos excepciones de comportamiento de la 015 se documentan como enmiendas de su spec, no de la constitución: FR-091 (pasarela en línea) y FR-174 (pago a nombre del propio pagador).
- La prueba `NingunProcesoAplicaPagosNiInformaResultados` sigue intacta.

## Project Structure

### Documentation (this feature)

```text
specs/021-canal-asociados-pagos/
├── spec.md
├── plan.md                   # este archivo
├── research.md               # R1–R19
├── data-model.md             # ERP (CDG_*) y base del canal
├── quickstart.md
├── enmienda-constitucion.md  # C0, propuesta
├── contracts/
│   ├── erp-api-de-servicio.md   # /api/canal/v1 (ERP → lo consume el canal)
│   ├── canal-api.md             # API pública, webhook e interna del canal
│   ├── erp-administracion.md    # /api/channel + pantallas + permisos
│   └── pasarela-y-app.md        # adaptador de pasarela y puente MAUI
├── checklists/requirements.md
└── tasks.md                  # /speckit-tasks
```

### Source Code (repository root)

```text
src/Core/IngenIA365ERP.Domain/
├── Entities/Channel/                 # ChannelSettings, LegalText, PaymentDestination, PublishedConcept,
│                                     # ChannelEnablement, ContactUpdate, PaymentNotice(+Item)
└── Enums/Channel/
src/Core/IngenIA365ERP.Application/
├── Channel/
│   ├── Settings/ Destinations/ Concepts/ Enablements/ ContactUpdates/   # administración
│   ├── Service/                      # consultas y comandos de /api/canal/v1
│   │   ├── GetChannelPayablesQuery.cs
│   │   ├── RegisterChannelPaymentCommand.cs     # → AplicacionNoInteractiva
│   │   └── ReverseChannelPaymentCommand.cs
│   ├── Integration/ConsumeAssociateWithdrawalForChannel.cs  # RetiroDeAsociadoAprobadoV1
│   └── Common/AuditoriaDelCanal.cs, ICanalSatelite.cs     # puerto hacia /internal/v1
└── Common/Execution/Actor.cs         # + ActorKind.Member, Actor.AsociadoPorCanal
src/Infrastructure/
├── IngenIA365ERP.Persistence/Configurations/Channel/        # CDG_*
├── IngenIA365ERP.Persistence.Migrations.{PostgreSql,SqlServer}/  # CanalDigital (aditiva)
├── IngenIA365ERP.Identity/Seed/ChannelPermissionCatalogSeeder.cs
└── IngenIA365ERP.ExternalChannels/Canal/ClienteDelCanal.cs  # ICanalSatelite (HTTP + token ES256)
src/Presentation/
├── IngenIA365ERP.API/
│   ├── Endpoints/Channel/            # /api/channel (usuarios)
│   ├── Endpoints/CanalServicio/      # /api/canal/v1 (servicio)
│   └── Security/CanalServicio/       # esquema, política, resolución de cooperativa, almacén de jti
└── IngenIA365ERP.Shared/Pages/Canal/ # seis pantallas
tests/                                # Domain, Application, Architecture, API.IntegrationTests (Canal/)

canal/                                # SATÉLITE (enmienda C0)
├── api/                              # Go
│   ├── cmd/canal/main.go
│   ├── internal/
│   │   ├── identidad/                # cuentas, activación, sesiones, dispositivos, webauthn, otp
│   │   ├── entidades/                # directorio, espejo de habilitaciones, arrastre
│   │   ├── conceptos/                # proxy de payables y simulación
│   │   ├── pagos/                    # intentos, referencia, máquina de estados, aviso al ERP
│   │   ├── pasarela/{placetopay,simulada}/
│   │   ├── erp/                      # cliente /api/canal/v1 + verificación del token del ERP
│   │   ├── mensajeria/{correo,sms,push}/
│   │   ├── auditoria/  cifrado/  trabajos/ (River)  http/ (rutas, middleware)
│   ├── migrations/                   # goose
│   ├── queries/                      # sqlc
│   ├── web/                          # build de canal/web embebido (embed)
│   └── Dockerfile
├── web/                              # React + Vite (PWA)
│   └── src/{pantallas,componentes,servicios,plataforma.ts,marca/}
├── app/                              # MAUI HybridWebView (.NET 10)
│   ├── Platforms/{Android,iOS}/      # biometría, App Links / Universal Links
│   └── Resources/Raw/wwwroot/        # copia del build de canal/web (no versionada)
├── docker-compose.yml                # PostgreSQL de desarrollo del canal
└── README.md
.github/workflows/canal.yml           # CI de canal/** (Go, web, imagen, digest a GitOps)
```

**Structure Decision**: el ERP sigue su estructura en capas con un módulo nuevo `Channel`. El satélite vive en
`canal/` en la raíz, fuera de `src/`, para que ni la solución `.slnx` ni el CI de .NET lo toquen. La app MAUI del
canal es un proyecto **distinto** de `IngenIA365ERP.App`, que es la app de funcionarios, y no entra en
`IngenIA365ERP.slnx`.

## Entregas y orden

| Entrega | Contenido | Depende de |
|---|---|---|
| **C0** | Enmienda de la constitución (dos revisores). Esquema `CanalServicio` en el ERP, `/api/canal/v1/tenants` y el esqueleto de `canal/` con CI y despliegue en DEV. | — |
| **C1** | Módulo Canal del ERP (configuración, textos, conceptos sin destino, habilitaciones, novedades); identidad completa del canal (activación, ingreso, códigos, passkeys PWA, selector, sesiones); consulta de conceptos; PWA en modo sólo consulta; atención básica. | C0 |
| **C2** | Destinos y credenciales; intentos, adaptador Placetopay y simulado, webhook, consulta periódica; `RegisterChannelPaymentCommand` y enmienda FR-091/FR-174; historial y comprobantes; certificación de Avalpay para COOFLOPAL. | C1 |
| **C2b** | Otros cobros (cuentas por cobrar comerciales, R16). | C2 |
| **C3** | Conciliación por llave (R4), reverso, atención completa, indicadores. | C2 |
| **C4** | Documentos (extractos, certificados), avisos por correo y Web Push, avisos de cuota próxima. | C1 (C2 para los de pago) |
| **C5** | App MAUI: puente, biometría, App Links, FCM, publicación en tiendas. | C2 |
| **C6** | Simulador y solicitud de crédito en línea (R17). | C1 |

Todas después del 01/12/2026 (D16), salvo C0, que puede adelantarse porque no toca a COOFLOPAL.

## Riesgos

- **Frecuencia de la consulta periódica en producción**: la guía de Avalpay habla de 24 h y la pasarela no reintenta
  la notificación. Se propone 15 min las primeras 24 h; se confirma con su analista antes de C2.
- **FR-174**: el aviso aplica sin una persona del ERP; si el segundo revisor de la enmienda lo objeta, la alternativa
  es una bandeja de confirmación, que contradice D11 y hay que llevar al dueño.
- **Passkeys en la app**: no funcionan dentro de HybridWebView. La app usa biometría nativa (R13); si el dueño
  quiere passkeys también en la app, habría que hacer la ceremonia en Custom Tabs.
- **Tipos de documento extranjeros** (PPT, PEP, PA) frente a los de la pasarela: se verifican en certificación.
- **Firma de la app** y cuentas de desarrollador: dependen del dueño; sin ellas, C5 no se publica.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Un segundo stack (Go + React + MAUI híbrido) fuera de .NET/Blazor | Decisión expresa del dueño (D2); se encauza con la enmienda C0, que confina el stack al satélite y le impone reglas | Hacer el canal como módulo .NET/Blazor: lo descartó el dueño |
| Segundo esquema de autenticación en el ERP (`CanalServicio`) | El ERP no tiene autenticación entre servicios y el satélite no tiene usuario del ERP | Un usuario técnico (prohibido en el código); mTLS (costo de operación sin ganancia sobre una red interna con NetworkPolicy) |
| Excepción a FR-174 de la 015 para el pago en línea | D11 del dueño: aplicación en línea sin revisión | Bandeja humana de confirmación: contradice D11 |
