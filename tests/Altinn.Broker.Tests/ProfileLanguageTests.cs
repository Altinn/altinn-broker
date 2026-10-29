using Altinn.Broker.Application;

using Xunit;

namespace Altinn.Broker.Tests;

public class ProfileLanguageTests
{
    [Theory]
    [InlineData("nb", "nb")]
    [InlineData("NN", "nn")]
    [InlineData(" en ", "en")]
    [InlineData("no", "nb")]
    public void TryNormalize_AcceptsAltinnLanguageCodes(string value, string expected)
    {
        var normalized = ProfileLanguage.TryNormalize(value, out var language);

        Assert.True(normalized);
        Assert.Equal(expected, language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sv")]
    public void TryNormalize_RejectsUnknownLanguages(string? value)
    {
        Assert.False(ProfileLanguage.TryNormalize(value, out _));
    }

    [Theory]
    [InlineData("UL=1044", "nb")]
    [InlineData("UL=1033", "en")]
    [InlineData("UL=2068", "nn")]
    [InlineData("UL%3D1044", "nb")]
    [InlineData("FOO=1&UL=1033", "en")]
    public void FromPersistentContext_ReadsUl(string cookie, string expected)
    {
        Assert.Equal(expected, ProfileLanguage.FromPersistentContext(cookie));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FOO=1")]
    [InlineData("UL=9999")]
    public void FromPersistentContext_ReturnsNullWhenUlIsMissing(string? cookie)
    {
        Assert.Null(ProfileLanguage.FromPersistentContext(cookie));
    }

    [Fact]
    public void MergePersistentContext_ReplacesUlAndKeepsOtherSegments()
    {
        Assert.Equal("FOO=1&UL=1033", ProfileLanguage.MergePersistentContext("FOO=1&UL=1044", "1033"));
        Assert.Equal("FOO=1&UL=1044", ProfileLanguage.MergePersistentContext("FOO=1", "1044"));
        Assert.Equal("UL=2068", ProfileLanguage.MergePersistentContext(null, "2068"));
        Assert.Equal("UL=1044", ProfileLanguage.MergePersistentContext("UL%3D2068", "1044"));
    }
}
