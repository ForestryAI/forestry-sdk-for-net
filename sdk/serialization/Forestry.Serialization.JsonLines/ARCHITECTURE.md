# ARCHITECTURE.md — Forestry Serialization JsonLines

The truth is the spec-kit silo of Forestry.Serialization: `../Forestry.Serialization/specs/005-json-lines/`
(spec, research R58-R67). This page summarizes the design; it does not add to it.

## 1. Purpose

The first format that writes telemetries, and the reference for anyone writing their own (see the
[README](README.md#writing-your-own-format)). JSON Lines was chosen first because it is readable by
people, needs very little code on top of the core, and is easy to test by parsing each line back
(spec 005, developer 2026-11-27).

## 2. Shape

```text
TelemetrySerialization.Serialize(telemetry, Stream | PipeWriter, SerializeJsonLinesOptions)
  -> SerializeJsonLinesOptions.CreateWriter()  -> JsonLinesTelemetryWriter (one per call)
  -> the type definition's object converter walks the members (core):
       WriteStartTelemetry  -> Utf8JsonWriter.WriteStartObject
       WriteName / WriteNull -> WritePropertyName / WriteNull (names JSON-encoded once per type)
       converter.Write       -> WriteValue / WriteEnumName / WriteRaw -> Utf8JsonWriter
       WriteEndTelemetry     -> WriteEndObject, Flush, "\n", Reset
```

- **`Utf8JsonWriter`** (R65) owns commas, escaping (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`: only
  what JSON requires) and validation. It buffers, so the writer refuses direct `IBufferWriter` use (R66).
- **Enums** (R58, R59): a member-aware factory creates an `EnumConverter<TEnum>` per enum type and form,
  names unless `[JsonLinesEnumForm]` on the member says numbers.
- **Refusals**: NaN and infinity (no JSON number); an incomplete `WriteRaw` value.

## 3. Not here

Reading JSON Lines; options (naming, omitting nulls, gzip); collecting (#141); partitions, which are the
client's (#147).
