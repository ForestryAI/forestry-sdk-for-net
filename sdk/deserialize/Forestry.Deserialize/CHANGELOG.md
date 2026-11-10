# Release History

## 1.0.0-beta.1 (Unreleased)

### Features Added
- Shred a document into flat rows in one forward pass: one instance of your own class per occurrence,
  handed out as soon as it is complete (spec 001 US1, #57).
- Work with rows without knowing their class, through `Row` and its fields (spec 001 US2, #58).
- Synchronous input over memory and strings, asynchronous input over a pipe or stream (spec 001 US3,
  #60).
- Policies: `DefinitionPolicy` chooses a row type's members and `EnumerationPolicy` acts as rows are
  built and enumerated, attached by attribute or through `DeserializeOptions.Policies` (spec 001 US4,
  #59).

### Bugs Fixed
- A failed `DeserializeOptions.MakeReadOnly()` remembered that a type had no converter, so adding the
  missing converter and calling it again still failed (#74).
