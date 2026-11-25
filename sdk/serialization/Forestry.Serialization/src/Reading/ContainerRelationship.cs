namespace Forestry.Serialization.Reading
{
    /// <summary>
    /// Where a telemetry sits in the document's containment (spec 002 FR-014, research R21): its own id and the
    /// id of the telemetry that was open around it when it was constructed
    /// </summary>
    /// <remarks>
    /// The parent is held by id, not by reference, so a kept telemetry never keeps a parent that left the
    /// context in memory (C1); <see cref="DeserializeContext.Parent(ContextTelemetry)"/> finds the parent only
    /// while it is still in the context. Other relationship groups (keys and references, graphs) will bring
    /// their own relationship types.
    /// </remarks>
    /// <param name="Id">The telemetry's id, unique within one enumeration, in construction order</param>
    /// <param name="ParentId">The innermost open telemetry's id when the telemetry was constructed, or null for none</param>
    public readonly record struct ContainerRelationship(long Id, long? ParentId);
}
