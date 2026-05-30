# Phase 0 — Research: Fase 0 Cimientos técnicos

**Feature**: 001-cimientos-tecnicos | **Date**: 2026-05-28

Cada subsección sigue el formato Decision / Rationale / Alternatives considered. Las decisiones son vinculantes para esta fase y solo se revisan si un Tasks downstream identifica un bloqueo real.

---

## 1. Hashing de contraseña

**Decision**: BCrypt.Net-Next 4.x con cost factor **11**, work factor calibrado anualmente.

**Rationale**:
- El estándar técnico de la constitución (`§Estándares Técnicos / Seguridad`) exige BCrypt cost ≥ 11.
- BCrypt es resistente a fuerza bruta por GPU y tiene 25 años de criptoanálisis activo.
- Cost 11 entrega ~80–120 ms por verificación en hardware servidor moderno: suficiente para frenar fuerza bruta sin penalizar UX.
- BCrypt.Net-Next es la implementación .NET mantenida (Bouncy Castle indirectamente) — 0 CVE conocidas activas.

**Alternatives considered**:
- **Argon2id (Konscious.Security.Cryptography)**: técnicamente superior (memory-hard), pero el spec original menciona "argon2/bcrypt" como alternativos. El ecosistema .NET para Argon2 es menos maduro, requiere ajuste fino de m/t/p y no aporta ventaja material sobre BCrypt cost 11 para esta fase. Descartado.
- **PBKDF2 (la default de ASP.NET Identity)**: insuficiente — vulnerable a aceleración por GPU. Descartado.
- **scrypt**: técnicamente válido, pero menor ecosistema .NET y curva de adopción mayor. Descartado.

---

## 2. Segundo factor (MFA) por TOTP

**Decision**: TOTP RFC 6238 implementado con **Otp.NET 1.4.x**; periodo 30 s; ventana de validación ±1 (es decir, ±30 s); inscripción mediante QR code generado por **QRCoder 1.6.x**; secreto base32 de 160 bits guardado en `SEC_Users.MfaSecret` cifrado con DataProtection.

**Rationale**:
- TOTP por aplicación autenticadora es el estándar internacional (NIST SP 800-63B AAL2) y no requiere infraestructura SMS adicional ni costos por mensaje.
- Otp.NET es la librería .NET madura para HOTP/TOTP; cero dependencias externas a runtime.
- QRCoder es pure-C# y permite renderizar el QR como SVG inline en Blazor sin servir imágenes.
- La ventana ±1 absorbe la desincronización de reloj del cliente sin abrir la ventana de ataque a más de 90 s.
- Cifrar el secreto en reposo con DataProtection evita que un dump de BD revele todos los factores.

**Alternatives considered**:
- **OTP por SMS**: rechazado por costo, vulnerabilidad a SIM swap (NIST lo desaconsejó en SP 800-63B v3) y dependencia de proveedor externo.
- **Push notification (Microsoft Authenticator API)**: requiere Azure AD; fuera del alcance y crea acoplamiento a un proveedor cloud.
- **WebAuthn/FIDO2**: técnicamente superior, pero exige dispositivos hardware compatibles que las cooperativas pequeñas no tienen. Posible Fase futura.

---

## 3. Códigos de respaldo MFA

**Decision**: Al inscribir MFA, generar **10 códigos de 10 caracteres alfanuméricos sin ambigüedad** (sin O/0, I/1/l). Cada código se entrega al usuario UNA VEZ (descarga + impresión) y se persiste el **hash BCrypt cost 11** (no el código en claro). Cada código es de uso único: una vez consumido, queda marcado como usado y no puede revertirse. El usuario puede regenerar el set desde su perfil; al hacerlo, los códigos anteriores quedan invalidados en bloque.

**Rationale**:
- 10 códigos × 10 chars (base32) = 50 bits de entropía por código, suficiente para resistir adivinanza online (el rate limit del login los protege de fuerza bruta).
- Hash BCrypt asegura que un dump de BD no entrega códigos válidos.
- Uso único + invalidación en bloque al regenerar elimina ambigüedad sobre qué código sigue siendo válido.
- El número 10 es el estándar de facto (Google, GitHub, AWS).

**Alternatives considered**:
- **Hash SHA-256 sin salt**: rechazado — ataques con rainbow tables/dictionaries triviales para alfabeto reducido.
- **Códigos de 6 dígitos**: rechazado — muy pocos bits (20), vulnerable a colisiones.
- **Códigos infinitos regenerables sobre demanda**: rechazado — confunde al usuario sobre cuáles funcionan; mejor un set fijo de 10.

---

## 4. JWT y manejo de sesión

**Decision**:
- **Access token**: JWT firmado con **RS256**, TTL 30 minutos, claims `sub` (User.PublicId), `tenant_id` (Tenant.PublicId), `branch_id` (opcional), `roles` (array de Role.PublicId), `perm_ver` (versión del set de permisos efectivos del usuario) y `jti` único.
- **Refresh token**: opaco (256 bits base64url), TTL 12 horas, persistido en Redis con metadata `{ userPublicId, tokenFamily, issuedAt, expiresAt }`. Rotación obligatoria: cada `POST /api/auth/refresh` entrega un nuevo refresh + nuevo access, e invalida el refresh anterior; **si un refresh ya invalidado se reutiliza, se considera robado y se invalida la familia completa** (logout forzoso de todas las sesiones derivadas).
- **Claves RS256**: par RSA 2048 administrado por DataProtection con rotación trimestral; ambos sets vigentes coexisten durante la transición.
- **Inactividad**: el cliente Blazor mantiene un timer de 30 minutos sobre eventos de UI; al expirar sin actividad, dispara logout local + revocación del refresh en backend.

**Rationale**:
- RS256 separa firma (privada en backend) de verificación (clave pública distribuible) — útil cuando hay múltiples servicios consumiendo el mismo JWT.
- TTL corto del access reduce ventana de explotación; refresh rotation con detección de reutilización es la mitigación estándar (OAuth 2.0 Security Best Practice, RFC 9700).
- `perm_ver` permite invalidar tokens cuando cambian permisos sin esperar a su expiración natural si se decide ser estricto (fuera de scope; el spec acepta hasta 30 min).

**Alternatives considered**:
- **HS256 (HMAC)**: más simple pero requiere compartir el secreto entre verificadores; descartado por previsión de servicios auxiliares.
- **Refresh token como JWT**: descartado — sería self-contained, no se puede revocar instantáneamente sin blacklist.
- **Sin refresh token (re-login periódico)**: descartado — fricción UX inaceptable para administradores que pasan jornada en el panel.

---

## 5. Concurrencia optimista

**Decision**: Todas las entidades editables persisten una columna `RowVersion (timestamp / rowversion)` en SQL Server, mapeada por EF Core con `.IsRowVersion()`. Los Commands de actualización aceptan `string ConcurrencyToken` (base64 del RowVersion previo) en el body; el handler compara el token recibido con el actual y, en caso de discrepancia, devuelve `Result.Failure("Concurrency.StaleRowVersion", msg)` con la identidad del último editor y su timestamp para que el frontend pueda mostrar mensaje accionable.

**Rationale**:
- `rowversion` es el mecanismo nativo SQL Server, sin costo de mantenimiento (el motor lo actualiza atómicamente).
- EF Core trata el `DbUpdateConcurrencyException` resultante como excepción tipada que el `LoggingBehavior` puede capturar para emitir el `Result` correcto.
- Permite escalar a más de un nodo sin coordinador adicional.

**Alternatives considered**:
- **Sello manual (`Version int` incrementado por handler)**: descartado — el handler tendría que coordinar el incremento, y queda expuesto a race conditions si dos transacciones leen y escriben simultáneamente.
- **Bloqueo pesimista (`SELECT FOR UPDATE`)**: descartado — UX rígida (bloquea registros mientras un usuario edita), no escala bien y crea deadlocks.
- **Last-write-wins**: descartado en el clarify del spec (Pregunta 3).

---

## 6. Audit log en MongoDB

**Decision**:
- Colección **`audit_events`** en base MongoDB independiente (`ingenia365_audit`).
- Estructura del documento:

```json
{
  "_id": ObjectId,
  "publicId": "Guid",
  "tenantId": "Guid",
  "userId": "Guid",
  "username": "string",
  "ip": "string",
  "userAgent": "string",
  "operationType": "Create|Update|Delete|Restore|Login|...",
  "entityType": "string",
  "entityPublicId": "Guid",
  "changes": [ { "field": "Username", "before": "x", "after": "y" } ],
  "occurredAt": ISODate,
  "createdAt": ISODate,
  "schemaVersion": 1
}
```

- **Índices**:
  - `(tenantId, occurredAt: -1)` — listado por empresa con orden descendente (caso 80%).
  - `(tenantId, userId, occurredAt: -1)` — filtro por usuario.
  - `(tenantId, entityType, entityPublicId, occurredAt: -1)` — historial de una entidad concreta.
  - `(createdAt)` con `expireAfterSeconds: 157_680_000` — TTL 5 años exactos.
- **Sharding**: clave `tenantId` (hashed) para distribución balanceada a futuro; en piloto se desactiva sharding (cluster mono-nodo) pero los índices ya están diseñados para soportarlo.
- **Inmutabilidad**: el `IAuditWriter` solo expone `AppendAsync`; cualquier operación `UpdateOne`/`DeleteOne` es bloqueada por una `IMongoCollection<>` wrapper que la rechaza. Adicionalmente, la cuenta de aplicación MongoDB tiene rol `appendOnly` que niega update/delete a nivel de servidor (defense-in-depth).
- **Pipeline AuditBehavior**: el behavior captura `Command`/`Query` exitosos (decisión: solo Commands de modificación + Queries marcadas con `[Auditable]`) y publica un evento `AuditEvent` al `IAuditWriter` antes de retornar al endpoint. Si el writer falla, el handler también falla (FR-026, SC-003).

**Rationale**:
- MongoDB con TTL es la solución más simple para retención larga sin scripts de purga.
- La estructura de documento es self-contained (no requiere joins para investigaciones forenses).
- El TTL de 5 años se calcula `5 * 365 * 24 * 3600 = 157_680_000` segundos.
- Sharding hashed por tenant distribuye la carga sin riesgo de hot spots.

**Alternatives considered**:
- **SQL Server `AUD_AuditEntries`**: descartado — penaliza con escrituras adicionales en cada transacción y no escala bien para el volumen acumulado proyectado (90 GB / 100 tenants / 5 años).
- **Elasticsearch**: superior para búsqueda full-text pero TCO mucho mayor y requiere expertise operativo extra; descartado para Fase 0.
- **EventStore / Kafka**: válido si se quisiera análisis de stream, pero excesivo para Fase 0.

---

## 7. Exportación del audit log

**Decision**:
- **CSV**: `CsvHelper 31.x`, separador `,`, encoding UTF-8 con BOM (para Excel), columnas: `Timestamp UTC`, `Usuario`, `IP`, `Operación`, `Entidad`, `Identificador`, `Cambios JSON`. Streamed para evitar cargar la consulta completa en memoria.
- **PDF firmado**:
  - Renderizado con **QuestPDF 2024.x**: portada con datos de la empresa (NIT, razón social), rango exportado, usuario solicitante, total de registros y huella SHA-256 de los datos crudos. Tabla con encabezado fijo, paginación tipo "X de N".
  - **Firma**: hash HMAC-SHA256 del PDF rendido calculado con una clave de firma administrada por DataProtection, embebido como metadato del PDF y reflejado en la página de portada. No es firma digital PKCS#7 (no aplica certificado X.509 cliente — se trata de huella de integridad reproducible internamente y verificable por el operador del SaaS).
  - **Tamaño objetivo**: hasta 100.000 filas por export; más allá, el sistema fuerza el formato CSV o segmentación por rango.

**Rationale**:
- CSV con BOM abre limpio en Excel sin sobrescribir caracteres acentuados.
- QuestPDF es la opción .NET para PDF programático con mejor calidad tipográfica y licencia community-friendly.
- HMAC-SHA256 con clave en DataProtection cumple el objetivo regulatorio de detectar manipulación posterior sin la complejidad de PKI completa; el operador puede recalcular el HMAC y compararlo.

**Alternatives considered**:
- **iTextSharp**: licenciamiento AGPL/comercial complica la distribución.
- **Firma PKCS#7 con certificado X.509**: justificable a futuro para entrega externa formal, pero exige gestión de certificados que excede Fase 0.
- **Exportar a JSON-LD para integraciones programáticas**: pospuesto a fase posterior cuando haya consumidores externos.

---

## 8. Almacén de adjuntos cifrado

**Decision**:
- Nuevo proyecto **`IngenIA365ERP.Storage`** con `IBlobStore` y dos implementaciones: `LocalEncryptedFileStore` (default) y `S3CompatibleEncryptedBlobStore` (opcional, sin trabajo en Fase 0 más allá de la interfaz).
- **Esquema de cifrado**: envelope encryption:
  - DEK (Data Encryption Key) AES-256-GCM por archivo, generada con CSPRNG (`RandomNumberGenerator.Fill`).
  - DEK se cifra con una KEK (Key Encryption Key) gestionada por **DataProtection** del backend (rotación trimestral nativa).
  - El archivo en disco contiene: `[12 bytes nonce][16 bytes auth tag][ciphertext]`. La DEK cifrada y el nonce viven en la tabla `COM_Attachments` (campos `EncryptedDataKey`, `Iv`).
- **Limites por defecto**: tamaño máximo 25 MB por archivo, tipos MIME permitidos: `application/pdf`, `image/jpeg`, `image/png`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document`. Configurables por tenant.
- **Hash de integridad**: SHA-256 del clear-text se calcula al subir y se persiste en `COM_Attachments.Sha256`; se recalcula al descargar y se valida antes de devolverlo (defense contra corrupción silenciosa).

**Rationale**:
- Envelope encryption es el patrón estándar (AWS KMS, Azure Key Vault, Google KMS) — permite rotar KEK sin recifrar petabytes.
- AES-256-GCM provee confidencialidad **y** autenticidad en una sola primitiva.
- DataProtection en backend es suficiente para Fase 0; cuando llegue infra cloud, sustituir KEK por KMS sin cambiar el resto.
- Sistema de archivos local mantiene la fase auto-contenida (cooperativas on-premise pueden operar sin objeto-store).

**Alternatives considered**:
- **Almacenar archivos en SQL Server VARBINARY(MAX)**: descartado — penaliza el backup, hincha la BD, viola el principio de almacenes especializados.
- **Cifrar solo a nivel de filesystem (BitLocker/LUKS)**: descartado — protege solo contra robo físico del disco, no contra acceso desde el host.
- **Sin cifrado en piloto, añadir después**: rechazado por FR-033 explícito.

---

## 9. Servicio de notificaciones (correo + in-app)

**Decision**:
- **Correo**: **MailKit 4.x** con `SmtpClient` async. Reintentos: 3 con backoff exponencial (`1s, 4s, 16s` + jitter 0–500 ms). Tras agotar reintentos, persistir el fallo en `NOT_NotificationDeliveryFailures` con motivo, payload mínimo y timestamp para diagnóstico (sin cuerpo del correo para evitar PII innecesaria).
- **In-app**: `NOT_Notifications` (SQL Server) + SignalR hub `INotificationsHub` para push en tiempo real al frontend. Si el cliente no está conectado, la notificación queda persistida y se entrega al próximo refresh.
- **Plantillas**: archivos `.cshtml` (RazorEngineCore) en `IngenIA365ERP.Storage/EmailTemplates/`, una plantilla por tipo de evento (`AccountLocked`, `PasswordChanged`, `MfaReset`, `RoleAssigned`).

**Rationale**:
- MailKit es el cliente SMTP .NET de referencia, con soporte completo para STARTTLS, autenticación moderna y manejo robusto de errores.
- SignalR es nativo en ASP.NET Core 10, encaja con Blazor Server y soporta WASM vía cliente JS.
- Razor templating reutiliza la infraestructura de renderizado existente.

**Alternatives considered**:
- **SendGrid / Mailgun**: descartado por dependencia externa y costo en piloto; sí está habilitado el reemplazo de `IEmailSender` para Fase posterior si se decide.
- **Polling del frontend cada N segundos**: descartado — peor UX y carga innecesaria; SignalR es estrictamente mejor.

---

## 10. Bloqueo distribuido, blacklist y caché de claims (Redis)

**Decision**:
- **Refresh token store**: clave `refresh:{tokenHash}` → `{ userPublicId, tokenFamily, issuedAt, expiresAt }`, TTL 12 h.
- **Blacklist de tokens revocados (jti)**: clave `revoked:{jti}` → `1`, TTL = TTL restante del JWT al revocarlo.
- **MFA reset coordinator**: clave `mfa-reset:{requestPublicId}` con set de aprobadores; se completa solo si dos aprobadores distintos firman dentro de 24 h.
- **Permission claims cache**: clave `perms:{userPublicId}:{tenantId}:{permVer}` → set de strings `Entidad.Accion`, TTL 30 min (alineado con access TTL). Se invalida ante cualquier cambio de rol del usuario (publish a pub/sub `perms:invalidate`).

**Rationale**:
- Redis ya es parte del stack (constitución) — reutilizarlo evita un nuevo backing store.
- Las TTL alineadas con los tokens evitan blacklists que crezcan sin límite.
- El permission cache acelera FR-019 (cambio de permisos visible en máximo 30 min) sin recomputar en cada request.

**Alternatives considered**:
- **In-memory por nodo**: rechazado — incompatibilidad con multi-nodo.
- **SQL Server para blacklist**: rechazado — escritura caliente; Redis es el repositorio adecuado.

---

## 11. Política de contraseña configurable por tenant

**Decision**:
- Entidad `SEC_PasswordPolicies` por tenant con campos: `MinLength` (default 12, rango 8–32), `RequireUppercase` (default true), `RequireLowercase` (default true), `RequireDigit` (default true), `RequireSymbol` (default true), `ExpiryDays` (default 90, rango 30–180), `HistorySize` (default 5, rango 5–24), `LockoutThreshold` (default 5, rango 3–10), `LockoutMinutes` (default 15, rango 5–60).
- Validador `PasswordPolicyEnforcer` aplicado en `RegisterUserCommand`, `ChangePasswordCommand` y `AdminResetPasswordCommand`.
- Historial: tabla `SEC_PasswordHistory` con hash de las últimas N contraseñas por usuario.

**Rationale**:
- Una sola política por tenant elimina ambigüedad sobre qué regla aplica a quién.
- Los rangos previenen configuraciones absurdas (p. ej. ExpiryDays = 1 día).
- Historial con hash mantiene la propiedad de no-recuperabilidad mientras permite la verificación de reuso.

**Alternatives considered**:
- **Política global única**: descartado — el spec exige configurabilidad por empresa (FR-008/FR-009).
- **Política por rol**: sobreingeniería para Fase 0; no hay evidencia de necesidad.

---

## 12. Habeas data (versionado y consentimientos)

**Decision**:
- `COR_HabeasDataPolicyVersions`: versión semver, fecha de publicación, vigencia (`EffectiveFrom`, `EffectiveTo`), texto completo en markdown, hash SHA-256 del texto para integridad.
- `COR_HabeasDataConsents`: por titular (`PersonId`) y versión de política aceptada, timestamp UTC, evidencia (texto del consentimiento mostrado en el momento), IP, agente, estado (`Accepted`/`Revoked`), motivo de revocación opcional.
- Una persona puede tener múltiples consentimientos a lo largo del tiempo; el sistema considera "vigente" el último `Accepted` posterior a cualquier `Revoked`.
- Propagación de revocación: el evento `HabeasDataRevokedEvent` (MediatR notification) activa handlers en módulos consumidores futuros (asociados, nómina, contabilidad) para marcar el tratamiento como restringido.

**Rationale**:
- Versionado explícito de la política permite responder al regulador con la versión exacta que firmó cada titular.
- Hash del texto evita litigios sobre "qué decía esa versión".
- Modelado con `MediatR.INotification` mantiene desacoplado al módulo de habeas data de sus consumidores.

**Alternatives considered**:
- **Política única vigente**: descartado — un cambio de política no invalida automáticamente consentimientos previos, hay que poder probar qué firmó cada quien.
- **Consentimientos solo en MongoDB de auditoría**: descartado — los consentimientos son operacionales (no solo históricos): hay que consultarlos en flujos de negocio frecuentemente.

---

## 13. Pruebas de carga (100 concurrent users)

**Decision**:
- **NBomber 5.x**, escenario `Cimientos.Concurrency.100Users` con perfil mixto:
  - 60% reads: listado de usuarios, listado de roles, consulta audit log (paginada 50).
  - 30% writes: crear usuario, asignar rol, marcar notificación leída.
  - 10% subida/descarga de adjunto (PDF 200 KB).
- Métricas verificadas: `p95 < 2 s` (SC-002), error rate < 1%, throughput sostenido durante 10 min de ramp + 30 min de plateau.
- Dataset sintético: 1.000 usuarios pre-creados, 50 roles, 50.000 entradas de audit log pre-pobladas.
- Ejecución: pipeline CI nightly + on-demand antes de release de la fase.

**Rationale**:
- NBomber es nativo .NET, scripteable en C# y produce reportes JSON+HTML que se pueden archivar como artefactos.
- El perfil mixto refleja la realidad: las acciones de administración generan más lectura que escritura.
- 10 min ramp + 30 min plateau es suficiente para detectar memory leaks ligeros y degradación por GC.

**Alternatives considered**:
- **k6 / Gatling**: válidos, pero requieren cambio de lenguaje (JS / Scala). Innecesario.
- **JMeter**: madurez probada, pero overhead operativo más alto que NBomber para Fase 0.

---

## 14. Provisión de schema-per-tenant

**Decision**:
- Cada nuevo tenant aprovisiona un schema SQL Server nuevo (`tnt_{shortcode}`) y migra las tablas operativas desde el template versionado en `database/schema/`.
- La rutina de aprovisionamiento es idempotente (verifica existencia del schema y tablas antes de crear) y se ejecuta vía `Application.Admin.Tenants.ProvisionTenantSchemaCommand`.
- El `TenantResolutionMiddleware` (existente) usa el claim `tenant_id` del JWT para resolver el `Tenant` desde `ADM_Tenants` y configurar la connection string / search path del request.
- La base `IngenIA365ERP_Admin` (tablas `ADM_*`) sigue siendo única y global.

**Rationale**:
- Schema-per-tenant es vinculante por la constitución (Principio IV); la fase debe materializar la mecánica completa antes de instalar módulos de negocio.
- Idempotencia permite re-ejecutar la rutina ante fallas parciales sin corromper schemas existentes.

**Alternatives considered**:
- **Database-per-tenant**: superior aislamiento físico pero overhead operativo (backup/restore N veces) demasiado alto para 100 tenants proyectados. Decisión consistente con la constitución.
- **Row-level por DiscriminatorColumn**: rechazado expresamente por la constitución.

---

## 15. Resumen de paquetes nuevos

| Paquete | Versión | Uso |
|---------|---------|-----|
| `Otp.NET` | 1.4.* | TOTP RFC 6238 |
| `QRCoder` | 1.6.* | QR code para inscripción MFA |
| `CsvHelper` | 31.* | Export CSV del audit log |
| `QuestPDF` | 2024.* | Export PDF firmado del audit log |
| `MailKit` | 4.* | SMTP con reintentos |
| `NBomber` | 5.* | Pruebas de carga |
| `NSubstitute` | 5.* | Mocks en Application.Tests (si no estuviera ya) |
| `Testcontainers.MsSql` | 4.* | SQL Server efímero en integration tests |
| `Testcontainers.MongoDb` | 4.* | MongoDB efímero en integration tests |
| `ArchUnitNET` | 0.* | Architecture.Tests (ya en uso) |
| `RazorEngineCore` | 2025.* | Plantillas HTML de correo |

Cero paquetes con licencia incompatible. Todas las versiones se fijan en `Directory.Packages.props` central tras `/speckit-tasks`.

---

## NEEDS CLARIFICATION resolved

Ninguno. La sesión `/speckit-clarify` cerró las 5 preguntas críticas. Todas las decisiones técnicas de este documento son consistentes con el spec y la constitución v1.0.0.
