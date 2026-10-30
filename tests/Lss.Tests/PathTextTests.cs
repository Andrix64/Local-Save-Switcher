namespace Lss.Tests;

public sealed class PathTextTests
{
    private static string Sep => Path.DirectorySeparatorChar.ToString();

    [Fact]
    public void MixedAndDoubledSeparatorsBecomeOne()
    {
        string expected = string.Join(Sep, "a", "b", "c");

        Assert.Equal(expected, PathText.Normalize("a\\b/c"));
        Assert.Equal(expected, PathText.Normalize("a//b\\\\c"));
        Assert.Equal(expected, PathText.Normalize("a/b/c/"));
        Assert.Equal(expected, PathText.Normalize("a\\b\\c\\"));
    }

    [Fact]
    public void RootsKeepTheirSeparator()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("C:\\", PathText.Normalize("C:/"));
            Assert.Equal("C:\\Users\\x", PathText.Normalize("C:\\\\Users/x\\"));
            Assert.Equal("\\\\server\\share\\f", PathText.Normalize("\\\\server\\\\share/f"));
        }
        else
        {
            Assert.Equal("/", PathText.Normalize("/"));
            Assert.Equal("/home/x", PathText.Normalize("//home\\x/"));
        }
    }

    [Fact]
    public void EmptyStaysEmpty() => Assert.Equal("", PathText.Normalize(""));

    [Fact]
    public void SameComparesAfterNormalising()
    {
        Assert.True(PathText.Same("a/b/", "a\\b"));
        Assert.False(PathText.Same("a/b", "a/c"));
        Assert.Equal(OperatingSystem.IsWindows(), PathText.Same("A/B", "a\\b"));
    }
}
