using Forestry.Deserialize.Attributes;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Stand-in for a StanForD document type until a real Forestry.StanForD schema project
    /// exists - just enough shape ([Element] names on plain properties) to exercise
    /// TypeDefinition/PropertyDefinition reflection without tying it to XML.
    /// </summary>
    [Collection("Machine")]
    public sealed class TestingMachine
    {
        [Element("MachineId", "Machine")]
        public string MachineId { get; set; } = string.Empty;

        [Element("MachineType", "Machine")]
        public string MachineType { get; set; } = string.Empty;
    }
}
