namespace Lss.Tests;

public sealed class CloudTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    // La cartella del gioco di TestFolder è Root\870780: Root fa da cartella dell'account.
    private string SharedConfig => Path.Combine(folder.Root, "7", "remote", "sharedconfig.vdf");

    private static string Config(string appBlock) =>
        "\"UserRoamingConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n" +
        "\t\t\t\t\"apps\"\n\t\t\t\t{\n" + appBlock + "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n}\n";

    [Fact]
    public void CloudDisabledForControl()
    {
        TestFolder.Write(SharedConfig, Config("\t\t\t\t\t\"870780\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"cloudenabled\"\t\t\"0\"\n\t\t\t\t\t}\n"));

        Assert.Equal(CloudState.Disabled, folder.CreateManager().GetCloudState());
    }

    [Fact]
    public void CloudDisabledOnlyForAnotherGameMeansEnabled()
    {
        TestFolder.Write(SharedConfig, Config("\t\t\t\t\t\"12345\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"cloudenabled\"\t\t\"0\"\n\t\t\t\t\t}\n"));

        Assert.Equal(CloudState.Enabled, folder.CreateManager().GetCloudState());
    }

    [Fact]
    public void MissingConfigMeansEnabled()
    {
        Assert.Equal(CloudState.Enabled, folder.CreateManager().GetCloudState());
    }

    [Fact]
    public void GameWithoutCloudFilesMeansDisabled()
    {
        SaveManager manager = folder.CreateManager();
        File.Delete(folder.CacheFile);

        Assert.Equal(CloudState.Disabled, manager.GetCloudState());
    }

    [Fact]
    public void BrokenConfigMeansUnknown()
    {
        TestFolder.Write(SharedConfig, "\"apps\" { \"870780\" { \"cloudenabled\" ");

        Assert.Equal(CloudState.Unknown, folder.CreateManager().GetCloudState());
    }

    [Fact]
    public void Vdf_HandlesEscapesAndComments()
    {
        Vdf.Node root = Vdf.Parse("// commento\n\"a\" { \"b\" \"x\\\"y\" \"c\" { \"d\" \"1\" } }");

        Assert.Equal("x\"y", Vdf.Find(root, "a", "b"));
        Assert.Equal("1", Vdf.Find(root, "c", "d"));
        Assert.Null(Vdf.Find(root, "c", "manca"));
    }
}
