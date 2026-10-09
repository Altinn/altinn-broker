using Altinn.Broker.Common;

using Xunit;

namespace Altinn.Broker.Tests;

public class IdPortenAuthenticationLevelTests
{
    private const string Required = "idporten-loa-substantial";

    [Theory]
    [InlineData("idporten-loa-high", true)]
    [InlineData("idporten-loa-substantial", true)]
    [InlineData("eidas-loa-high", true)]
    [InlineData("eidas-loa-substantial", true)]
    [InlineData("idporten-loa-low", false)]
    [InlineData("selfregistered-email", false)]
    public void IsSufficient_ComparesLevelsByStrength(string reached, bool expected)
    {
        Assert.Equal(expected, IdPortenAuthenticationLevel.IsSufficient(reached, Required));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("idporten-loa-unknown")]
    [InlineData("IDPORTEN-LOA-HIGH")]
    public void IsSufficient_WithUnrecognisedValue_IsNeverSufficient(string? reached)
    {
        Assert.False(IdPortenAuthenticationLevel.IsSufficient(reached, Required));
    }

    [Fact]
    public void IsSufficient_WithUnrecognisedRequirement_IsNeverSufficient()
    {
        Assert.False(IdPortenAuthenticationLevel.IsSufficient("idporten-loa-high", "not-a-level"));
    }

    [Theory]
    [InlineData("idporten-loa-high", 4)]
    [InlineData("eidas-loa-high", 4)]
    [InlineData("idporten-loa-substantial", 3)]
    [InlineData("eidas-loa-substantial", 3)]
    [InlineData("idporten-loa-low", 2)]
    [InlineData("selfregistered-email", 0)]
    public void TryGetLevel_ResolvesTheDocumentedValues(string authenticationContext, int expectedLevel)
    {
        Assert.True(IdPortenAuthenticationLevel.TryGetLevel(authenticationContext, out var level));
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void TryGetLevel_WithUnknownValue_ReturnsFalse()
    {
        Assert.False(IdPortenAuthenticationLevel.TryGetLevel("something-else", out var level));
        Assert.Equal(-1, level);
    }
}
