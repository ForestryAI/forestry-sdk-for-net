# Forestry StanForD Harvesting

The Harvesting part of StanForD (`.hpr` documents), planned to split out of
[Forestry.StanForD](../Forestry.StanForD/README.md), which then keeps what all StanForD document
types share.

## Direction

This package will hold Harvesting-specific logic:

- **Deserializer policies**: Harvesting validation and policies for
  [Forestry.Deserialize](../../deserialize/Forestry.Deserialize/README.md), including reading the
  `Extension` elements written by the program that moves documents from the machine to Biometria.
  The Swedish forest industry calls it Sender, so Sender is the default option.
- **Core direction and answer policies**: Harvesting knowledge for the Core pipeline (today
  [Forestry.Turn](../../turn/Forestry.Turn/README.md)) that hands rows over as Parquet to Azure
  Storage, for example the partition path `/{year}/{month}/harvesting/{contract}` and file names by
  stem key range (`stem-{stem key start}-{stem key end}`, `log-{stem key start}-{stem key end}`),
  and the matching answer.

## Status

Not started: no source and no `.csproj` yet. Parquet writing comes first, see the
[SDK README](../../../README.md#direction).
