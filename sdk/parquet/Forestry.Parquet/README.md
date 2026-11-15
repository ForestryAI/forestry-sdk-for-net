# Forestry Parquet

A writer for [Apache Parquet](https://parquet.apache.org/) files, planned for later.

## Direction

To speed up the POC, Parquet writing starts in Forestry.Deserialize instead: it gets writing
functions, from a row to a similar flat format, and a Forestry.Deserialize.Parquet media package
writes rows as Parquet, at first possibly with [Parquet.Net](https://www.nuget.org/packages/Parquet.Net)
(still on NuGet, though its source repository is no longer public).

This package comes afterwards: a Parquet writer of its own, in the same spirit as
[Forestry.Xml](../../xml/Forestry.Xml/README.md) on the reading side. It only moves forward, writing
bytes straight to a `PipeWriter` or a stream that cannot seek (for example an upload to Azure
Storage), without allocating per value. Parquet suits that: row groups come first and the footer
last. It covers what flat rows need, so no nested columns, and has no dependency on
Forestry.Deserialize.

## Status

Not started: no source and no `.csproj` yet.
