# Forestry Deserialize Xml

StanForD XML support for [Forestry.Deserialize](../Forestry.Deserialize/README.md) — the concrete
media provider that lets the core package's streaming, schema-guided walk actually read a real
harvester/forwarder XML export instead of just describing one.

For how this package is put together internally — the custom span-based reader, why
`System.Xml.XmlReader` isn't used, and the narrow StanForD dialect it targets — see
[ARCHITECTURE.md](ARCHITECTURE.md). For the design history and the real `.hpr` sample analysis
behind those decisions, see [CLAUDE.md](../CLAUDE.md).

## Status

**Not usable yet — nothing in this package compiles.** Every type here (`Utf8XmlReader`,
`TokenType`, `ReaderState`, `DeserializeXmlOptions`, `ObjectDeserializer<T>`) is an early skeleton:
either an explicit `NotImplementedException` stub, a class missing required interface/abstract
members, or an unconditional no-op. See ARCHITECTURE.md §5 for the precise list of what's missing
in each file.

Until then, [Forestry.Deserialize](../Forestry.Deserialize/README.md)'s own README/tests are the
best reference for what actually works today — schema reflection and the core pipe-based walk,
neither of which is XML-specific.
