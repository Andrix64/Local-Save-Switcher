using System.Text;

namespace Lss.Tests;

/// <summary>
/// Le cartelle di salvataggio cloud del catalogo di Epic diventano regole, e il
/// catalogo (base64 di un array JSON) si legge senza errori anche se è rotto.
/// </summary>
public sealed class EpicCloudFolderTests : IDisposable
{
    private readonly TestFolder folder = new();

    public void Dispose() => folder.Dispose();

    [Fact]
    public void AppDataIsTheLocalFolderAndEpicIdIsAnyFolder()
    {
        SaveRule? rule = EpicCloudFolder.ToRule("{AppData}/FactoryGame/Saved/SaveGames/{EpicID}/");

        Assert.NotNull(rule);
        Assert.Equal("WinAppDataLocal", rule.Root);
        Assert.Equal("FactoryGame/Saved/SaveGames/*", rule.Path);
        Assert.Equal("*", rule.Pattern);
        Assert.True(rule.Recursive);
        Assert.Empty(rule.Platforms);
    }

    [Fact]
    public void TheSiblingsOfAppDataAreLocalLowAndRoaming()
    {
        // Il valore reale è scritto in minuscolo: i token non distinguono le maiuscole.
        SaveRule? lowRule = EpicCloudFolder.ToRule("{appdata}/../locallow/Dodge Roll/Enter the Gungeon/");
        SaveRule? roamingRule = EpicCloudFolder.ToRule("{AppData}/../Roaming/runic games/torchlight/save/");

        Assert.Equal("WinAppDataLocalLow", lowRule?.Root);
        Assert.Equal("Dodge Roll/Enter the Gungeon", lowRule?.Path);
        Assert.Equal("WinAppDataRoaming", roamingRule?.Root);
        Assert.Equal("runic games/torchlight/save", roamingRule?.Path);
    }

    [Fact]
    public void UserDirAndBackslashesBecomeMyDocumentsWithSlashes()
    {
        SaveRule? rule = EpicCloudFolder.ToRule("{UserDir}\\My Games\\TheSpectrumRetreat\\Saves\\");

        Assert.Equal("WinMyDocuments", rule?.Root);
        Assert.Equal("My Games/TheSpectrumRetreat/Saves", rule?.Path);
    }

    [Fact]
    public void OtherKnownTokensGiveTheirRoots()
    {
        Assert.Equal("WinSavedGames", EpicCloudFolder.ToRule("{UserSavedGames}/KingdomCome/")?.Root);
        Assert.Equal("WinUserProfile", EpicCloudFolder.ToRule("{UserProfile}/Documents/Cat Quest II/")?.Root);
        Assert.Equal("gameinstall", EpicCloudFolder.ToRule("{InstallDir}/Saves/")?.Root);
        Assert.Equal("Saves", EpicCloudFolder.ToRule("{InstallDir}/Saves/")?.Path);
    }

    [Fact]
    public void EpicIdWithAnotherCaseIsAnyFolderToo()
    {
        Assert.Equal("Autore/Gioco/*/x", EpicCloudFolder.ToRule("{UserDir}/Autore/Gioco/{EpicId}/x")?.Path);
    }

    [Theory]
    [InlineData("{Foo}/Saves/")]
    [InlineData("Saves/")]
    [InlineData("{AppData}/../other/Saves/")]
    [InlineData("{AppData}/Gioco/../../segreto/")]
    [InlineData("{InstallDir}/{Altro}/x")]
    public void UnknownOrUnsafeValuesGiveNoRule(string value)
    {
        Assert.Null(EpicCloudFolder.ToRule(value));
    }

    [Fact]
    public void CatalogGivesTheCloudFolderOfEachItem()
    {
        string file = Path.Combine(folder.Root, "catcache.bin");
        string json = "[{\"id\":\"abc\",\"title\":\"Gioco\",\"customAttributes\":{\"CanRunOffline\":{\"type\":\"STRING\",\"value\":\"true\"},"
                      + "\"CloudSaveFolder\":{\"type\":\"STRING\",\"value\":\"{InstallDir}/Saves/\"}}},"
                      + "{\"id\":\"def\",\"customAttributes\":{}}]";
        TestFolder.Write(file, Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

        var catalog = EpicCloudFolder.ReadCatalog(file);

        Assert.Equal(new[] { "abc" }, catalog.Keys);
        Assert.Equal("{InstallDir}/Saves/", catalog["abc"]);
    }

    [Fact]
    public void MissingOrBrokenCatalogIsEmpty()
    {
        string file = Path.Combine(folder.Root, "catcache.bin");

        Assert.Empty(EpicCloudFolder.ReadCatalog(file));

        TestFolder.Write(file, "non base64 !!!");
        Assert.Empty(EpicCloudFolder.ReadCatalog(file));

        TestFolder.Write(file, Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"non\":\"un array\"}")));
        Assert.Empty(EpicCloudFolder.ReadCatalog(file));
    }
}
