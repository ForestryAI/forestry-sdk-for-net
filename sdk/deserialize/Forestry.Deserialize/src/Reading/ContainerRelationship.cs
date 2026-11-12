namespace Forestry.Deserialize.Reading
{
    /// <summary>
    /// Where a row sits in the document's containment (spec 002 FR-014, research R21): its own id and the
    /// id of the row that was open around it when it was constructed
    /// </summary>
    /// <remarks>
    /// The parent is held by id, not by reference, so a kept row never keeps a parent that left the
    /// context in memory (C1); <see cref="DeserializationContext.Parent(ContextRow)"/> finds the parent only
    /// while it is still in the context. Other relationship groups (keys and references, graphs) will bring
    /// their own relationship types.
    /// </remarks>
    /// <param name="Id">The row's id, unique within one enumeration, in construction order</param>
    /// <param name="ParentId">The innermost open row's id when the row was constructed, or null for none</param>
    public readonly record struct ContainerRelationship(long Id, long? ParentId);
}
