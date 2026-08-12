# Forestry Deserialize Xml

General XML support for [Forestry.Deserialize](../Forestry.Deserialize/README.md) — the concrete
media provider that lets the core package's streaming, schema-guided walk actually read XML
instead of just describing one. StanForD harvester/forwarder XML exports are what created the need
for this and are the real documents it's developed and tested against, but the reader isn't
StanForD-specific — it targets XML generally.

For how this package is put together internally — the custom span-based reader and why
`System.Xml.XmlReader` isn't used — see [ARCHITECTURE.md](ARCHITECTURE.md). For the design history
and the real `.hpr` sample analysis behind those decisions, see [CLAUDE.md](../CLAUDE.md). Parts of
this package's design are adapted from Microsoft's open-source .NET runtime — see
[THIRD-PARTY-NOTICES.md](../../../THIRD-PARTY-NOTICES.md).

## Status

**Not usable yet, though the package builds clean.** `TokenType`/`Constants` now carry a real XML
token vocabulary, and `ReaderState` fully implements the core's `IReaderState<TState>` shape, but
the actual tokenizing (`Utf8XmlReader.Read()`) and walking (`ObjectDeserializer<T>
.TryReadNullableValue`, `BooleanDeserializer.TryReadNullableValue`, `DeserializeXmlOptions`) are
still stubs. See ARCHITECTURE.md §5 for the precise, current list of what's built vs. stubbed in
each file.

Until then, [Forestry.Deserialize](../Forestry.Deserialize/README.md)'s own README/tests are the
best reference for what actually works today — schema reflection and the core pipe-based walk,
neither of which is XML-specific.
