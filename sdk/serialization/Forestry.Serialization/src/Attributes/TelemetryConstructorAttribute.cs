namespace Forestry.Serialization
{
    /// <summary>
    /// Marks the constructor a telemetry type uses, public or non-public. A telemetry type uses exactly one
    /// constructor: without a mark its single public constructor is used, and more than one mark
    /// fails when definitions are built (research R5). Read by the core, not the format
    /// </summary>
    /// <remarks>
    /// <c>AllowMultiple = false</c> only stops the attribute appearing twice on the same constructor;
    /// two marked constructors of one class are rejected when definitions are built.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class TelemetryConstructorAttribute : Attribute
    {
    }
}
