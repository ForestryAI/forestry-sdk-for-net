# Forestry Deserialize Xml

StanForD XML support for [Forestry.Deserialize](../Forestry.Deserialize/README.md) — the concrete
media provider that lets the core package's streaming, schema-guided walk actually read a real
harvester/forwarder XML export instead of just describing one.

For how this package is put together internally — the custom span-based reader, why
`System.Xml.XmlReader` isn't used, and the narrow StanForD dialect it targets — see
[ARCHITECTURE.md](ARCHITECTURE.md). For the design history and the real `.hpr` sample analysis
behind those decisions, see [CLAUDE.md](../CLAUDE.md).

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
