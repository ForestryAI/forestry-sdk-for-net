using Forestry.Deserialize.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Tests
{
    /// <summary>
    /// Packaging gate (eng spec 001, constitution C2, tasks.md T024, T027): the rejected values of
    /// <see cref="DeserializeOptions.Policies"/> no existing test covers. Its default and overrides are
    /// covered by <see cref="PolicyPrecedenceTests"/>
    /// </summary>
    public sealed class PolicyOptionsTests
    {
        // Rejected: ArgumentNullException from each of Add(policy), Add<TRow>(policy) and
        // Add<TRow>(memberName, policy)
        [Fact]
        public void Policies_Rejects_ANullPolicyOnEveryAdd()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Policies.Add(null!));
            Assert.Throws<ArgumentNullException>(() => options.Policies.Add<Log>(null!));
            Assert.Throws<ArgumentNullException>(() => options.Policies.Add<Log>(nameof(Log.LogKey), null!));
        }

        // Rejected: a null member name is ArgumentNullException, an empty one ArgumentException
        [Fact]
        public void Policies_Rejects_ANullOrEmptyMemberName()
        {
            // Arrange
            TestingDeserializeOptions options = new();

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => options.Policies.Add<Log>(null!, new SetMarkerPolicy()));
            Assert.Throws<ArgumentException>(() => options.Policies.Add<Log>(string.Empty, new SetMarkerPolicy()));
        }

        // Rejected: InvalidOperationException from Add(policy) and Add<TRow>(memberName, policy);
        // Add<TRow>(policy) after locking is covered by OptionsTests
        [Fact]
        public void Policies_Rejects_TheOtherAddsAfterTheOptionsAreLocked()
        {
            // Arrange
            TestingDeserializeOptions options = new();
            options.Rows.Add<Log>();
            options.MakeReadOnly();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => options.Policies.Add(new SetMarkerPolicy()));
            Assert.Throws<InvalidOperationException>(() => options.Policies.Add<Log>(nameof(Log.LogKey), new SetMarkerPolicy()));
        }
    }
}
