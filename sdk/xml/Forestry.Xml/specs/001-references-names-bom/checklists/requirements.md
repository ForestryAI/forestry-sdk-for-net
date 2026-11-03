# Specification Quality Checklist: References, names compared in full, BOM

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-03
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

- The users are developers and the product is a reader API, so public names (`Utf8XmlReader`,
  `ReaderOptions.MaxNameLength`, `XmlException`, the references flag) and grammar productions are
  the user-facing contract, not implementation details, as in the deserialization silo's spec 001.
  How the scan is built (extending the evaluate-then-commit scan, reference state across segments)
  is left to research and plan.
- Developer decisions (C5, 2026-11-03): an entity declared in a DTD throws "not supported" (Story 1
  scenario 7); `MaxNameLength` bounds every name, with its XML doc listing them (FR-010).
- Character references: leading zeros skipped, at most 6 significant hex or 7 decimal digits,
  value a legal Char up to U+10FFFF, no option of its own (FR-005a). The name-stack size and
  `MaxValueLength` go to a later limits spec (Assumptions). No markers remain.
