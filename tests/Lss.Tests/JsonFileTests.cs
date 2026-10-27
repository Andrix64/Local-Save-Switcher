namespace Lss.Tests;

/// <summary>JsonFile: lettura e scrittura dei file JSON di LSS.</summary>
public sealed class JsonFileTests : IDisposable
{
    private sealed class Sample
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public List<string> Items { get; set; } = [];
    }

    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    private string PathOf(string name) => Path.Combine(folder.Root, name);

    [Fact]
    public void WriteThenReadGivesTheSameValues()
    {
        string path = PathOf("a.json");

        JsonFile.Write(path, new Sample { Name = "Andrea", Count = 3, Items = ["x", "è"] });
        Sample? read = JsonFile.Read<Sample>(path);

        Assert.NotNull(read);
        Assert.Equal("Andrea", read.Name);
        Assert.Equal(3, read.Count);
        Assert.Equal(["x", "è"], read.Items);
    }

    [Fact]
    public void TheFileIsIndentedUtf8WithoutBomAndLeavesNoTemporaryFile()
    {
        string path = PathOf("b.json");

        JsonFile.Write(path, new Sample { Name = "è" });

        byte[] bytes = File.ReadAllBytes(path);
        Assert.NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Contains("\n", File.ReadAllText(path));
        Assert.Contains("  \"Name\"", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteCreatesTheFolderAndReplacesTheFile()
    {
        string path = Path.Combine(folder.Root, "sotto", "c.json");

        JsonFile.Write(path, new Sample { Name = "uno" });
        JsonFile.Write(path, new Sample { Name = "due" });

        Assert.Equal("due", JsonFile.Read<Sample>(path)!.Name);
    }

    [Fact]
    public void MissingFileGivesNull()
    {
        Assert.Null(JsonFile.Read<Sample>(PathOf("non-esiste.json")));
    }

    [Fact]
    public void UnknownPropertiesAreIgnored()
    {
        string path = PathOf("d.json");
        File.WriteAllText(path, "{\"Name\":\"x\",\"Futuro\":{\"a\":1},\"Count\":2}");

        Sample? read = JsonFile.Read<Sample>(path);

        Assert.Equal("x", read!.Name);
        Assert.Equal(2, read.Count);
    }

    [Fact]
    public void BrokenFileIsKeptAsRottoAndGivesNull()
    {
        string path = PathOf("e.json");
        File.WriteAllText(path, "{ questo non è JSON");

        Assert.Null(JsonFile.Read<Sample>(path));

        Assert.False(File.Exists(path));
        Assert.Equal("{ questo non è JSON", File.ReadAllText(path + ".rotto"));
    }

    [Fact]
    public void ANullDocumentCountsAsBroken()
    {
        string path = PathOf("f.json");
        File.WriteAllText(path, "null");

        Assert.Null(JsonFile.Read<Sample>(path));
        Assert.True(File.Exists(path + ".rotto"));
    }
}
