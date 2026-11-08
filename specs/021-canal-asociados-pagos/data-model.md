# Data model — Feature 021

Dos almacenes, sin cruce físico entre ellos (enmienda, regla 1):

- **A. Base de cada cooperativa (ERP)**: tablas nuevas `CDG_*` y tres ajustes a tablas existentes. Toda entidad nueva
  hereda de `AuditableEntity`, tiene `PublicId` y filtro de borrado lógico (Principios VI y VII).
- **B. Base del canal** (`ingenia365_canal`, una para la plataforma): esquema propio del satélite en Go.

Nada de lo de B guarda saldos, movimientos ni datos contables (FR-030).

---

## A. ERP — base de cada cooperativa

### CDG_ChannelSettings — configuración del canal (versionada)

Una fila vigente por cooperativa; cambiar crea una versión nueva con `EffectiveFrom` (FR-014).

| Campo | Tipo | Regla |
|---|---|---|
| Version | int | consecutivo; único con `EffectiveFrom` |
| EffectiveFrom | datetime | |
| IsEnabled | bool | FR-001 |
| ChannelCode | char(3) | lo asigna la plataforma al encender; prefijo de las referencias (R12); inmutable |
| DisplayName, LogoAttachmentPublicId, PrimaryColor, SecondaryColor | | marca (FR-007); colores `#RRGGBB` |
| SupportPhone, SupportEmail, SupportHours | | FR-007 |
| PaymentSessionMinutes | int | 10–30, por defecto 15 (FR-008) |
| RequireOtpToPay | bool | por defecto false (FR-009) |
| InactivityMinutes | int | 5–15, por defecto 5 (FR-027) |
| EnablementPolicy | enum | `AllActiveAssociates`, `Manual`, `Both` (FR-010) |
| AllowNonAssociates | bool | FR-010 |
| SmsEnabled | bool | FR-011a |
| DueReminderDays | int | 0–15, por defecto 3 (FR-012) |
| SurplusPolicy | enum | `AdvanceElseCreditBalance` (por defecto), `AlwaysCreditBalance` (FR-013, R6) |

### CDG_LegalTexts — textos legales versionados

`Kind` (`Terms`, `DataPolicy`, `Faq`, `GatewayFaq`), `Version`, `Content` (Markdown), `PublishedAt`. La aceptación
del asociado guarda la versión (FR-020, FR-034). Una versión publicada no se edita: se publica otra.

### CDG_PaymentDestinations — destinos de pago

| Campo | Regla |
|---|---|
| Name | |
| Gateway | enum `Placetopay` (otras después) |
| SitePublicId | el sitio en el satélite (las credenciales viven allá, R10) |
| CollectionAgreementId | FK a `RCD_CollectionAgreements` con `Kind = Gateway`; de él salen la cuenta propia, las comisiones y el formato del archivo de conciliación |
| OfferPse, OfferCard | FR-006 |
| ReversalCutoffLocalTime | por defecto 20:00 (FR-052) |
| CredentialsStatus | `NotSet`, `Active`, `Failed` + `CredentialsCheckedAt`, `LoginLast4` (espejo informativo de R10) |

### CDG_PublishedConcepts — conceptos publicados

| Campo | Regla |
|---|---|
| Kind | `Loan`, `Contribution`, `ContractualSaving`, `ProgrammedSaving`, `CommercialReceivable` (C2b) |
| LineCode | línea de crédito o de captación; nulo = todas las de ese tipo |
| DestinationId | FK a `CDG_PaymentDestinations`; nulo = sólo consulta (FR-004) |
| AllowDue, AllowPartial, AllowPrepayment, AllowPayoff | modalidades (FR-003) |
| PartialMinimum | obligatorio si `AllowPartial` |
| CanCombine | si va con otros en un pago (FR-003, FR-032) |
| DefaultPrepaymentChoice | `ReduceTerm` / `ReduceInstallment`; sólo créditos (FR-032a) |
| PublishedDocuments | marcas: extracto, certificado de aportes, certificado tributario (FR-055) |

Único: (`Kind`, `LineCode`) entre vivos.

### CDG_ChannelEnablements — habilitaciones

| Campo | Regla |
|---|---|
| PersonId | FK a `COR_People` (Principio V: nada personal se copia aquí) |
| Status | `Enabled`, `Disabled` |
| EnabledAt | la fecha que decide la entidad validadora (D5) |
| Source | `Policy` (todo asociado activo), `Manual`, `Bulk` |
| DisabledReason | obligatorio al inhabilitar; `Withdrawal` cuando lo pone el retiro |
| ChangeSequence | long creciente por cooperativa; es el cursor del arrastre (R8) |

Único: `PersonId` entre vivos. Toda transición se audita (FR-011).

Transiciones: `—` → `Enabled` (política, manual o carga) · `Enabled` → `Disabled` (funcionario con motivo, retiro) ·
`Disabled` → `Enabled` (funcionario). Rehabilitar **conserva** el `EnabledAt` original sólo si nunca se perdió la
validación; si no, toma la fecha nueva.

### CDG_ContactUpdates — novedades de contacto

`PersonId`, `Field` (`Email`, `Mobile`), `NewValue` (enmascarado al listar), `ReportedAt`, `AcknowledgedBy/At`. La
cooperativa la ve en Personas y decide si actualiza `COR_People` por el flujo normal (FR-024, D7). Nunca la aplica sola.

### CDG_PaymentNotices — avisos recibidos

| Campo | Regla |
|---|---|
| Reference | la del canal (≤ 32); **único** (idempotencia, FR-044) |
| PersonId, DestinationId | |
| Total, GatewayRequestId, InternalReference, Authorization, Receipt, Franchise, PaymentMethodName, IssuerName, ApprovedAt | |
| PayerIp, PayerUserAgent | del asociado (FR-037, Principio X) |
| Items (hijo `CDG_PaymentNoticeItems`) | `ConceptKind`, `ProductPublicId`, `Modality`, `Amount`, `PrepaymentChoice?` |
| Outcome | `Applied`, `InReview` (saldo a favor o rechazo parcial) |
| ReceiptId | FK al `RCD_Receipts` creado |
| ReversedAt, ReversalReceiptId | FR-052 |

Inmutable salvo el bloque de reverso (Principio XI: lo contable vive en Recaudo y Contabilidad, que se reversan
con asiento espejo).

### Ajustes a tablas existentes

- `RCD_GatewayFileLines`: índice único `(GatewayFileId → AgreementId, GatewayReference)` filtrado a no borradas, y la
  `Key` de la línea con la forma `GW:{convenio}:{referencia}` (R4).
- `RCD_ChannelModalityRules`: fila para `Online` sembrada por la activación del canal (modalidad por defecto
  `CycleInstallment`, igual que hoy sin regla).
- `CreditApplications` (C6): `IntakeChannel`, `CreatedByUserId` nulable, `CreatedByMemberPublicId` (R17).

### Enumeraciones nuevas en código

`ActorKind.Member`, `ExecutionChannel.Channel` (R3), `ModuloDeAuditoria.Channel`, permisos `Channel.*`.

---

## B. Base del canal (`ingenia365_canal`)

Identificadores UUID v7. Nada se borra en flujo de usuario: se cambia el estado y la fecha (enmienda, regla 6).

### tenants

`tenant_public_id` (el `PublicId` del ERP), `channel_code`, `display_name`, `brand` (json), `erp_base_url`,
`is_enabled`, `enablements_cursor`, `last_sync_at`. Lo llena el arrastre del directorio
(`GET /api/canal/v1/tenants`).

### accounts — cuentas del canal

| Campo | Regla |
|---|---|
| id | UUID |
| document_type, document_number | **único** el par (FR-015); tipos del catálogo fijo del ERP |
| first_names, last_names | los de la entidad validadora al activar |
| email, mobile | verificados; nulos hasta verificar |
| password_hash | bcrypt 12 |
| status | `pending_activation`, `active`, `locked` |
| locked_until, failed_attempts | FR-022 |
| activated_at, validating_tenant_id | |
| sms_allowed | si alguna entidad habilitada tiene SMS |

Sin estado «cerrada» (aclaración 5).

### enablements — espejo de habilitaciones

`tenant_id`, `person_public_id`, `document_type`, `document_number`, `status`, `enabled_at`, `change_sequence`.
Único (`tenant_id`, `person_public_id`). La **entidad validadora** es la fila `enabled` de menor `enabled_at` para el
documento (D5); se calcula, no se guarda.

### legal_acceptances

`account_id`, `tenant_id`, `kind`, `version`, `accepted_at`, `ip`, `user_agent` (FR-020, FR-034). Sólo se anexa.

### devices

`account_id`, `kind` (`browser`, `android`, `ios`), `label`, `credential_hash` (credencial de dispositivo, R13),
`webauthn_credential` (PWA), `biometric_enabled`, `push_token`, `last_seen_at`, `revoked_at`.

### sessions

`account_id`, `device_id`, `token_hash`, `tenant_id` (la elegida; nula antes del selector), `created_at`,
`last_activity_at`, `expires_at` (≤ 8 h), `ended_at`, `end_reason`.

### otp_challenges

`account_id?`, `purpose` (`activation`, `new_device`, `recovery`, `contact_change`, `payment`), `channel` (`email`,
`sms`), `destination_hash`, `code_hash`, `expires_at` (5 min), `attempts` (≤ 3), `consumed_at`, `tenant_id` (para el
conteo de SMS por cooperativa, FR-011a).

### gateway_sites — sitios de pasarela

`id` (= `SitePublicId` del ERP), `tenant_id`, `gateway`, `base_url`, `login`, `secret_ciphertext`, `kek_version`,
`status`, `checked_at`, `payment_methods`. Nunca sale el secreto (R10).

### payment_attempts — intentos de pago

| Campo | Regla |
|---|---|
| id | UUID |
| reference | **único**, nunca reutilizado (FR-035, R12) |
| tenant_id, account_id, person_public_id, site_id, destination_public_id | |
| items | json: `conceptKey`, `productPublicId`, `modality`, `amount`, `prepaymentChoice`, `description`, `tax` |
| total, currency | `COP` |
| status | `created`, `pending`, `approved`, `rejected`, `failed`, `expired` |
| gateway_request_id, process_url, expires_at | |
| approval | json: `internalReference`, `authorization`, `receipt`, `franchise`, `paymentMethodName`, `issuerName`, `approvedAt` |
| buyer_ip, buyer_user_agent | FR-037 |
| erp_outcome | `not_sent`, `sending`, `applied`, `in_review`, `failed_retrying` + `erp_receipt_public_id` |
| reversed_at | FR-053 |

Restricción: a lo sumo un intento `created|pending` por (cuenta, concepto) (FR-041); doble clic → la misma fila
(FR-036, llave de idempotencia del cliente).

Transiciones: `created` → `pending` (sesión creada en la pasarela) → `approved | rejected | failed | expired`;
`expired` → `approved` si la pasarela aprueba después (caso borde). Estado final inmutable salvo `reversed_at`.

### attempt_events

`attempt_id`, `source` (`return`, `webhook`, `probe`, `reconciliation`, `reversal`), `gateway_status`, `raw` (json
sin datos de tarjeta), `at` (FR-043). Sólo se anexa.

### contact_change_outbox

Avisos de cambio de contacto pendientes por cooperativa (River los entrega con reintentos).

### audit_log

`at`, `account_id?`, `tenant_id?`, `actor` (`member`, `erp_staff`, `system`), `action`, `subject`, `ip`,
`user_agent`, `data` (json sin secretos). Sólo se anexa; retención ≥ 5 años (enmienda, regla 6).

### River (tablas de la librería)

Trabajos: `send_payment_notice`, `probe_pending_attempts`, `pull_enablements`, `full_resync`,
`send_contact_update`, `send_notification`, `due_reminders`.
