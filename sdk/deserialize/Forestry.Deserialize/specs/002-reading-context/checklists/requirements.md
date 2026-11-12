# Specification Quality Checklist: Deserialization context

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-11-11
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

- The user is a developer of a library, so `Row`, row classes and policies are what they see (as in
  spec 001); internal abstractions (store, enumerators, row builder) are left to the plan.
- Defaults taken without a marker, for the developer to confirm in review or `/speckit-clarify`:
  a row type and a member from the same node name fail at lock (edge case); a member node sets
  the innermost open row that has it (FR-013); unmatched named values are ignored (US3-3).
- Revision 2026-11-11 (developer review): examples made neutral (`Site > Order > Line > Price`);
  StanForD kept only as traceability under Driving needs; being in the context separated from
  being enumerated (FR-002, FR-003); US3 made media-neutral (named values and own value, XML maps
  attributes and text; FR-020). New defaults to confirm: enumerated and lifetime are decided per
  row at completion (not fixed per type at lock); a row that is enumerated and not kept leaves
  the context before it is handed out (FR-004, changed 2026-11-12).
- Added 2026-11-11 (developer idea): set members tracked per row, separate from closed (FR-007,
  US1-8, US1-9); later requirements renumbered by one. How it is stored (e.g. a bitmap
  with member positions fixed at lock) is left to the plan. Default to confirm: a member set by an
  empty node counts as set.
- Added 2026-11-12 (developer review): isolation (FR-021, US1-11 to US1-13); FR-004 changed so a
  row not kept leaves the context before it is handed out. Options for the plan: read-only once
  enumerated (frozen flag), `init`-only members set by the package, a copy handed out only for
  rows both enumerated and kept, or contract only. Only the copy covers both directions without
  constraining how developers write rows.
- Added 2026-11-12 (developer review): US2 is containment (parent-child and siblings from
  structure); each row records its parent (FR-014) and policies query parent, children and
  siblings (FR-015, US2-5, US2-6); FR-014 onwards renumbered by two (numbers above are current).
  Assumptions name three relationship groups: containment (this spec), keys and references, and
  graphs (later); forward references out of scope. Confirmed by the developer 2026-11-12: with
  containment, a row's parent is the innermost open row when it is constructed, whatever its type.
- Confirmed by the developer 2026-11-12: a member node sets the innermost open row that has it
  (FR-013); unmatched named values are ignored (US3-3); enumerated and lifetime are decided per row
  at completion. Also confirmed: same node name for a row type and a member fails at lock (members
  are flattened to primitives, so a node is either a row or a member, never both); a member set by
  an empty node counts as set (FR-007).
- Changed by the developer 2026-11-12 (plan review): an empty or bare row-type node is a row like
  any other (its presence may be a flag); default add to and remove from the context, other
  handling by policies. Replaces the earlier "bare node constructs no row" (edge case, FR-002).
  Context limit default: about 100 rows (research R28). Concurrent enumerations on one options
  instance deferred: 001's guard stays (FR-011, US1-10 and SC-003 rewritten).
  FR-009 (policies see the media position) removed: a media path would tie policies to one
  media; policies learn where they are from rows (research R27, Rejected).
- Items marked incomplete require spec updates before `/speckit-clarify` or `/speckit-plan`
