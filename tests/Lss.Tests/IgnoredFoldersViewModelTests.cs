using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Finestra delle cartelle che non sono salvataggi (M4): elenco e "Proponila di nuovo".
/// AppData.Root punta a una cartella di test.
/// </summary>
[Collection("AppData")]
public sealed class IgnoredFoldersViewModelTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly string savedDataRoot = AppData.Root;

    public IgnoredFoldersViewModelTests()
    {
        AppData.Root = folder.Data;
    }

    public void Dispose()
    {
        AppData.Root = savedDataRoot;
        folder.Dispose();
    }

    private string DataFolder => Path.Combine(folder.Data, "steam", "111", "123450");

    [Fact]
    public void ListsGlobalAndGameFoldersWithTheirScope()
    {
        var store = new IgnoredFolders(folder.Data);
        store.AddGlobal("{WinAppDataLocal}/Discord");
        store.AddForGame(DataFolder, Path.Combine(folder.Root, "logs"));

        var model = new IgnoredFoldersViewModel(DataFolder, "Sandfall");

        Assert.False(model.IsEmpty);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal("{WinAppDataLocal}/Discord", model.Items[0].Folder);
        Assert.Equal(Strings.IgnoredScopeAll, model.Items[0].Scope);
        Assert.True(model.Items[0].IsForAll);
        Assert.Equal("Sandfall", model.Items[1].Scope);
        Assert.False(model.Items[1].IsForAll);
    }

    [Fact]
    public void ProposeItAgainRemovesEachKind()
    {
        var store = new IgnoredFolders(folder.Data);
        store.AddGlobal("{WinAppDataLocal}/Discord");
        store.AddForGame(DataFolder, Path.Combine(folder.Root, "logs"));
        var model = new IgnoredFoldersViewModel(DataFolder, "Sandfall");

        model.Items[1].RemoveCommand.Execute(null);

        Assert.Single(model.Items);
        Assert.Empty(store.ForGame(DataFolder));

        model.Items[0].RemoveCommand.Execute(null);

        Assert.True(model.IsEmpty);
        Assert.Empty(model.Items);
    }

    [Fact]
    public void EmptyListSaysSo()
    {
        var model = new IgnoredFoldersViewModel(DataFolder, "Sandfall");

        Assert.True(model.IsEmpty);
        Assert.Equal(Strings.IgnoredFoldersEmpty, model.EmptyText);
    }

    [Fact]
    public void TheGameWindowShowsOnlyTheGameFoldersAndTheLssOneOnlyTheGlobalOnes()
    {
        var store = new IgnoredFolders(folder.Data);
        store.AddGlobal("{WinAppDataLocal}/Discord");
        store.AddForGame(DataFolder, Path.Combine(folder.Root, "logs"));

        var game = new IgnoredFoldersViewModel(DataFolder, "Sandfall", IgnoredScope.Game);
        var all = new IgnoredFoldersViewModel("", "", IgnoredScope.All);

        Assert.Equal("Sandfall", Assert.Single(game.Items).Scope);
        Assert.Equal("{WinAppDataLocal}/Discord", Assert.Single(all.Items).Folder);
    }
}
