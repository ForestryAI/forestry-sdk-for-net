# StanForD 2010 schemas

Skogforsk's published StanForD 2010 XML schemas, kept at the **latest version, V4p1**, for the POC
(developer, 2026-11-13). Row types and policies follow them (constitution A2). Each schema is also
here as `*_no_documentation.xsd`, the same schema without its annotations.

Older versions are not kept. The real Harvesting documents found so far declare version 3.5, so a
type built from V4p1 is checked against them, not assumed valid (A2). Which document versions are
read is spec 001's FR-019, a separate decision from which schema files are kept here.

| Area | Schemas (`_V4p1.xsd`) |
|---|---|
| Harvesting | `HarvestedProduction`, `HarvestedDeltaProduction`, `TotalHarvestedProduction`, `HarvestingQualityControl` |
| Forwarding | `ForwardedProduction`, `ForwardingObjectInstruction`, `ForwardingDeliveryInstruction`, `ForwardingQualityControl` |
| Felling and bunching | `FelledBunchedProduction`, `FellingBunchingObjectInstruction`, `FellingBunchingQualityControl` |
| Skidding and yarding | `SkiddedProduction`, `SkiddingObjectInstruction`, `YardedProduction`, `YardingObjectInstruction` |
| Instructions | `ObjectInstruction`, `ObjectGeographicalInstruction`, `ProductInstruction`, `SpeciesGroupInstruction`, `StemVolumeTableInstruction`, `DesignInstruction`, `UserDefinedDataInstruction` |
| Reports and monitoring | `ObjectGeographicalReport`, `OperationalMonitoring`, `OperationalMonitoringCodeList` |
| Shared | `StanForD2010CommonDefinitions`, `StanForD2010Envelope`, `StanForD2010EnvelopeAcknowledgement` |

The POC reads Harvesting only (`HarvestedProduction`, spec 001).
