# Specification Quality Checklist: Recaudo y Tesorería

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-22
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- **Quedan tres marcas de aclaración** (el máximo permitido), todas de alcance y con recomendación:
  - **C1 (FR-021)**: caja de Recaudo y caja del punto de venta de la 012, ¿separadas, una sola o caja mixta?
  - **C2 (FR-091)**: canales en línea del primer día, ¿sólo archivos o también pasarela en línea?
  - **C3 (FR-161)**: módulo de Tesorería heredado, ¿retiro con guarda, consulta de sólo lectura o migración de
    pendientes antes de retirarlo?
  Se resuelven con `/speckit-clarify`; la especificación funciona con la opción recomendada mientras tanto.
- **Revisión**: dos pasadas propias contra esta lista. La primera corrigió tres fugas de implementación (nombres de
  motores de base de datos y de la tecnología de las pantallas, salvo en la cita textual del pedido del dueño) y un
  error de redacción; la segunda agregó el orden de activación (Recaudo y Tesorería antes que Cartera y Captaciones,
  FR-150). No hubo verificación adversarial con subagentes; se recomienda antes de `/speckit-plan`.
- **Detalle técnico permitido, como en la 013 y la 014**: el contexto describe el estado del módulo heredado y de los
  contratos vigentes por su función (sin nombres de tablas, clases ni rutas) para fundamentar el retiro y la
  adopción. Los requisitos hablan en términos de negocio.
- **Pendiente de confirmar por el dueño**: decisiones por defecto D1 a D32, interpretaciones I-1 a I-3, conflictos C1 a
  C11 con otras features y las preguntas P1 a P6 y P9. No son marcas: la especificación funciona con ellas.
- **Pendiente del oficial de cumplimiento, la contadora y el tributarista**: P7, P8, P10 a P12; los valores se
  siembran «pendiente de validar».
- **Sin el nombre del sistema anterior**: verificado; se habla de «lo heredado» y de «cifras de referencia».

- 2026-10-22: las tres aclaraciones (C1, C2, C3) y la confirmación de D2 quedaron resueltas por el dueño (opción recomendada en todas); no quedan marcas.
