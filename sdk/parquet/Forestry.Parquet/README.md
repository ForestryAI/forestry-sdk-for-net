# Forestry Parquet

A forward-only writer for [Apache Parquet](https://parquet.apache.org/), the dominant flat columnar
format read by big data resources. Nothing is built yet; this README states the goals. The SDK's
[README](../../../README.md#big-data-clients) explains the whole path, from a device document to rows
that feed a client's pipeline and on to big data.

## Direction

- **JSON Lines first.** For the POC, rows are written as [JSON Lines](https://jsonlines.org/), which
  .NET writes forward and compresses as a stream with what it has built in. Parquet comes later.
- **A writer of its own.** In the same spirit as [Forestry.Xml](../../xml/Forestry.Xml/README.md) on
  the reading side: UTF-8, forward only, writing straight to a `PipeWriter` or a stream that cannot
  seek (for example an upload). It covers what flat rows need, so no nested columns, and has no
  dependency on Forestry.Deserialize. No third-party Parquet library is planned.
- **Writing is a pipeline directive.** A client's writing directive owns a format writer: JSON Lines
  now, Parquet later. Appending a row is a cheap call, held to the same standard as deserialization
  policies (no allocation per value); closing a row group or file, and handing it on to be sent, is
  the directive's work per chunk.

## Rows in, row groups out

Parquet is columnar: within a row group, all of one column's pages come before the next column's. So
rows go in one at a time, but bytes come out per row group:

- **Per row**, each member's value is encoded into its own column's buffer. Values that repeat, such
  as dimensions copied onto every row, cost a few bits each with dictionary encoding. What is held is
  encoded, compressed pages, not rows.
- **Per row group**, the columns are written one after another.
- **Per chunk**, one or more row groups and then the footer make one file. A chunk ends on size, so
  files come out large enough for big data without compacting small ones afterwards.

The buffer of a row group is a deliberate allocation, sized by the chunk; nothing is allocated per
value.

## Who does what

| Who | Does |
| :--- | :--- |
| **This package** | The format: schema, encodings, pages, column chunks, row groups, statistics and the footer |
| **A codec, plugged in** | Compresses each page. GZIP is built into .NET; Snappy or ZSTD can come from a third-party package behind a small codec interface; uncompressed needs nothing |
| **The client or resource** | When chunks are sent, and any compaction of files already written |

Compression cannot be applied to a finished file: Parquet compresses each page, and the footer records
the codec and where every page starts. A gzipped Parquet file is no longer readable as Parquet.

## What writing Parquet involves

- **Schema when the options lock.** Each member becomes a column with a physical type (`BOOLEAN`,
  `INT32`, `INT64`, `DOUBLE`, `BYTE_ARRAY`, `FIXED_LEN_BYTE_ARRAY`) and a logical type (`STRING`,
  `DECIMAL`, `TIMESTAMP`, `DATE`, `UUID`). Flat rows keep this simple: no nesting means no repetition
  levels, and a nullable member needs only a definition level of 0 or 1.
- **Encodings**: plain, dictionary for values that repeat, and the run-length/bit-packing hybrid for
  definition levels.
- **Pages**, each with a header, compressed by the codec.
- **Statistics**: minimum, maximum and null count per column chunk, which let engines skip blocks.
- **The footer, last.** It holds the schema, known when the options lock, plus what only writing
  tells: where each column chunk starts and how long it is, row counts and statistics. So it is
  prepared at the lock, filled in while writing, and written after the last row group, followed by its
  length and the closing `PAR1`.
- **Thrift.** Page headers and the footer use Thrift's compact encoding, which only needs writing,
  never reading.

## Types that need a decision

- **Decimals.** A Parquet `DECIMAL` column has a fixed precision (total digits) and scale (digits right
  of the decimal point), and precision decides the storage: `INT32` up to 9 digits, `INT64` up to 18,
  a fixed-length byte array beyond. A C# `decimal` carries its scale per value, so each decimal member
  declares its precision and scale, with an attribute on the member or an option, as names in a
  document are declared for XML. A decimal member without one fails when the options lock, and a value
  that does not fit fails while writing rather than being rounded silently.
- **Times.** A `DateTimeOffset` becomes a UTC timestamp, so its offset is not kept.

## Status

Not started: no source and no `.csproj` yet. It comes after JSON Lines writing and the first local
client; see [where the POC stands](../../../README.md#where-the-poc-stands).
