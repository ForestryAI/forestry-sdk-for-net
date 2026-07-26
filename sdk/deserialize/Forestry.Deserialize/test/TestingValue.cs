using System.Text;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Simple <see cref="Value"/> for testing, since the mutating members are protected internal
    /// </summary>
    public sealed class TestingValue : Value
    {
        public TestingValue(string name, string? rawValue = null)
        {
            Name = name;

            if (rawValue is not null)
            {
                SetRawValue(Encoding.UTF8.GetBytes(rawValue));
            }
        }

        public TestingValue WithDimension(string name, string value)
        {
            AddDimension(name, value);
            return this;
        }

        public TestingValue WithDepth(int depth) => WithDimension(Dimension.Names.Depth, depth.ToString());

        public TestingValue WithNamespace(string ns) => WithDimension(Dimension.Names.Namespace, ns);
    }
}
