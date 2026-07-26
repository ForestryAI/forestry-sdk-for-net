namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Reader driven by a fixed sequence of steps, each either producing a <see cref="Value"/> or
    /// throwing, so tests can exercise both a well-behaved stream and read failures without a
    /// real media parser.
    /// </summary>
    public sealed class TestingReader : IReader
    {
        private readonly Func<Value>[] _steps;

        private int _index = -1;

        public TestingReader(params Func<Value>[] steps)
        {
            _steps = steps;
        }

        public Value Current { get; private set; } = null!;

        public bool Read()
        {
            _index++;

            if (_index >= _steps.Length)
            {
                return false;
            }

            // Advance before invoking the step, so a throwing step still leaves this reader
            // positioned at the next one - satisfying IReader.Read's shunt-aside contract.
            Current = _steps[_index]();
            return true;
        }
    }
}
