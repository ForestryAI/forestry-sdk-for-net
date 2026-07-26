# Forestry Deserialize

Forestry Deserialize reads large documents — the primary case today is StanForD XML from
harvesting and forwarding machines — and streams data out rather than loading a whole file
into memory. A multi-gigabyte harvester log should cost roughly the same, memory-wise, to
process as a small one.

The package is split by concern:

- **Forestry.Deserialize** — the media-agnostic core: reflection-driven `TypeDefinition`s,
  `Deserializer`/`DeserializerFactory`, and the `Dimension`/`Value` model that carries data out
  of a stream.
- **Forestry.Deserialize.Xml** — the first concrete media provider, reading StanForD XML.
  JSON or another custom format would be a sibling provider built the same way.

## Concepts

- **`Deserializer`** — knows how to read one shape (a type, a collection element type, a
  dictionary key/value) and how to build the `TypeDefinition` for it via reflection.
- **`TypeDefinition`** / **`PropertyDefinition`** — the reflected shape of a target type,
  computed once per `DeserializeOptions` and reused across reads.
- **`Dimension`** — a named piece of context attached to a value (e.g. which machine, which
  section of the source document it came from), not the value's business data itself.
- **`Value`** — a single deserialized unit, carrying its raw bytes plus its dimensions.

## Current shape: one `Value` at a time

Reading currently walks the source stream and yields a single `Value` per step through
`ValueAsyncEnumerator`. That's a reasonable default for "don't load the whole document," but in
practice most consumers don't want isolated scalar values — they want a field group that only
makes sense together, e.g. everything StanForD reports for one machine, or one full log
telemetry entry.

**This is being reconsidered.** The likely direction is to yield clumps — a record made of the
fields that belong to one logical unit — instead of one value at a time, while keeping the same
streaming guarantee (a clump is read and released before the next one starts). If you're
building against this package, treat the per-`Value` enumeration API as unstable.

## Status

- `Forestry.Deserialize` (core): attribute-driven type reflection (`Element`, `Collection`) and
  the deserializer/type-definition plumbing are in place; `ValueAsyncEnumerator` is a stub
  (`MoveNextAsync` always returns `true`, `DisposeAsync` is not implemented) — reading isn't
  wired end-to-end yet.
- `Forestry.Deserialize.Xml`: first `ObjectDeserializer` exists but predates the current
  `Deserializer` base shape in core, so the two are out of sync. Needs to be brought current
  once the clump-based read shape above is settled, rather than fixed twice.
- StanForD 2010 and StanForD Classic: no format-specific mapping yet — this section will
  document field/dimension mappings once the XML provider reads end-to-end.

## Generics

`TypeDefinition` and friends are built once per `DeserializeOptions` via reflection, so a type
is only walked once no matter how many documents you deserialize with the same options instance.
