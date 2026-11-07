using Xunit;

namespace Forestry.Deserialize.Tests;

// Throwaway for eng spec 001 quickstart US1 step 2 (T019): CI must name this failing test.
// Reverted before PR #76 merges.
public class CiThrowawayTests
{
    [Fact]
    public void CiThrowaway_ItShould_FailOnPurpose()
    {
        // Arrange, Act, Assert
        Assert.Fail("Deliberate failure: quickstart US1 step 2");
    }
}
