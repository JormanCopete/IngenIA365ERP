# Specification Quality Checklist: Cartera financiera — captaciones

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-14
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

- **Aclaraciones resueltas por el dueño el 2026-10-14** (sección Clarifications):
  - **Q1**: todos los módulos se terminan antes de que salga COOFLOPAL;
  - **Q2**: se retira todo lo heredado de ahorros, aportes y CDT y se renueva lo que se necesite;
  - **Q3**: sin ventanilla provisional; esta feature va cuando termine la Cartera financiera;
  - **P1, P25, P27**: todo configurable partiendo de la norma colombiana; GMF con una sola cuenta exenta
    por persona y configurable hasta su eliminación.
- **Revisión del 2026-10-14**: el dueño pidió que las captaciones sean parte integral de la Cartera
  financiera; se reescribió como extensión de la Cartera (líneas con naturaleza colocación, captación y
  aportes; entregas K7 a K11) con el fundamento de `investigacion-financiera.md`.
- **Pendiente de confirmar por el dueño**: las decisiones por defecto D1 a D33, las interpretaciones
  constitucionales I-1 a I-5 y las preguntas abiertas (P13 a P20, P22 a P24, P28). No son marcadores: la especificación funciona con
  ellas mientras el dueño no diga otra cosa.
- **Pendiente de la contadora y el abogado**: P2 a P12 y P26 (norma y tributario). Los valores citados son los
  de la investigación del 2026-10-14; se siembran como «pendiente de validar» (FR-014).
- **Detalle de implementación permitido**, como en la 013: el contexto y las enmiendas nombran el estado
  actual del código (entidades conservadas, rutas, la consulta `IConsultaDeAportes`) para fundamentar el
  retiro y la integración. Los requisitos hablan en términos de negocio.
- **Sin el nombre del sistema anterior**: verificado; la spec habla de «lo heredado» y de «cifras de
  referencia».
- **Revisión**: una sola pasada propia contra esta lista. A diferencia de la 013, no hubo verificaciones
  adversariales con subagentes; se recomiendan antes de /speckit-plan. Los ejemplos numéricos de la investigación se comprobaron con un cálculo independiente.
