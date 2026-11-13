# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- Shred a document into flat rows in one forward pass: one instance of your own class per occurrence,
  handed out as soon as it is complete (spec 001 US1, #57).
- Work with rows without knowing their class, through `Row` and its fields (spec 001 US2, #58).
- Synchronous input over memory and strings, asynchronous input over a pipe or stream (spec 001 US3,
  #60).
- Policies: `DefinitionPolicy` chooses a row type's members and `EnumerationPolicy` acts as rows are
  built and complete (`Construct`, `Set`, `Complete`, `Clear`), attached by attribute or through
  `DeserializeOptions.Policies` (spec 001 US4, #59; reshaped by spec 002).
- The deserialization context (`DeserializationContext`), one per enumeration and passed to every
  enumeration policy method: rows are in it while open, and at completion policies decide, each at
  most once, whether a row comes out (`ContextRow.Enumerate`) and how long it stays
  (`ContextRow.Keep` with a `Lifetime`, `DeserializationContext.Release`); queries by row type
  (`Rows<TRow>()`), the set members of a row (`ContextRow.IsSet`), updates through
  `ContextRow.Set`, and a row both enumerated and kept handed out as a copy (spec 002 US1, #100).
- `DeserializeOptions.MaxContextRows` limits the rows kept in the context (default 100); going past
  it throws a `DeserializationContextLimitException` listing the kept rows per type (spec 002 US1,
  #100).
- Rows of several types nested in one pass, each recording its parent
  (`ContextRow.Container`, a `ContainerRelationship`), with `Parent`, `Ancestors`, `Children` and
  `Siblings` on the context (spec 002 US2, #101).

### Other Changes
- The policy surface changed before this version's release (spec 002): `EnumerationPolicy.Enumerate`
  became `Complete`, every method takes the deserialization context, and `next` passes what the
  step needs. Options with enumeration policies still serve one enumeration at a time.

### Bugs Fixed
- A failed `DeserializeOptions.MakeReadOnly()` remembered that a type had no converter, so adding the
  missing converter and calling it again still failed (#74).
