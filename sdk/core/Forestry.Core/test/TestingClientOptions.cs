namespace Forestry.Core.Tests
{
    /// <summary>
    /// Concrete client options for exercising the pipeline in tests
    /// </summary>
    public class TestingClientOptions : ClientOptions
    {
        public TestingClientOptions(Turning turning) : base(turning) { }
    }
}
