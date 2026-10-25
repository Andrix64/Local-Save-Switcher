namespace Lss.Tests;

public sealed class GlobTests
{
    [Theory]
    [InlineData("*", "a.sav", true)]
    [InlineData("*.sav", "slot1.sav", true)]
    [InlineData("*.sav", "slot1.dat", false)]
    [InlineData("S????.sl2", "S0001.sl2", true)]
    [InlineData("S????.sl2", "S001.sl2", false)]
    [InlineData("a*b*c", "a-x-b-y-c", true)]
    [InlineData("a*b*c", "a-x-c-y-b", false)]
    [InlineData("*.*", "a.sav", true)]
    [InlineData("save", "save", true)]
    [InlineData("save", "saves", false)]
    [InlineData("", "", true)]
    public void MatchesLikeAShellPattern(string pattern, string name, bool expected)
    {
        Assert.Equal(expected, Glob.IsMatch(pattern, name, ignoreCase: false));
    }

    [Fact]
    public void IgnoreCaseMatchesOtherCase()
    {
        Assert.True(Glob.IsMatch("My Games", "my games", ignoreCase: true));
        Assert.False(Glob.IsMatch("My Games", "my games", ignoreCase: false));
    }

    [Fact]
    public void HasWildcardFindsStarAndQuestionMark()
    {
        Assert.True(Glob.HasWildcard("a*"));
        Assert.True(Glob.HasWildcard("a?"));
        Assert.False(Glob.HasWildcard("abc"));
    }
}
