using Lss.App.Services;
using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Scelta a mano della cartella dei salvataggi (Altri giochi) e i testi dei
/// giochi non gestiti. Dialoghi finti; AppData.Root punta a una cartella di test.
/// </summary>
[Collection("AppData")]
public sealed class SaveFolderChoiceTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakeDialogService dialogs = new();
    private readonly string savedDataRoot = AppData.Root;

    public SaveFolderChoiceTests()
    {
        AppData.Root = folder.Data;
    }

    public void Dispose()
    {
        AppData.Root = savedDataRoot;
        folder.Dispose();
    }

    private string Home => Path.Combine(folder.Root, "home");
    private string Documents => Path.Combine(Home, "Documents");
    private string SettingsFile => Path.Combine(folder.Data, "steam", "111", "123450", "cartella.ini");

    private static GameInfo Game(SupportState support = SupportState.SavesElsewhere, string reason = "") =>
        new("steam", "123450", "Sandfall", "", "", "", "", support, "", "", SaveSource.None, reason);

    private static GameAccount Account => new("steam", "111", "Andrix", "", null);

    private FakeProvider Provider(params string[] protectedFolders)
    {
        var provider = new FakeProvider { Id = "steam" };
        provider.Launchers.AddRange(protectedFolders);
        return provider;
    }

    [Fact]
    public async Task ChosenFolderIsSavedAfterConfirmation()
    {
        string chosen = Path.Combine(folder.Root, "mine");
        TestFolder.Write(Path.Combine(chosen, "slot.sav"), "x");
        dialogs.FolderToPick = chosen;
        dialogs.ConfirmAnswer = true;

        bool saved = await SaveFolderChoice.ChooseAsync(dialogs, Game(), Account, Provider());

        Assert.True(saved);
        var loaded = new GameSaveSettings(Path.GetDirectoryName(SettingsFile)!);
        loaded.Load();
        Assert.Equal(chosen, loaded.Folder);
        Assert.Equal(SaveSource.Chosen, loaded.Origin);
    }

    [Fact]
    public async Task DeclinedConfirmationSavesNothing()
    {
        string chosen = Path.Combine(folder.Root, "mine");
        TestFolder.Write(Path.Combine(chosen, "slot.sav"), "x");
        dialogs.FolderToPick = chosen;
        dialogs.ConfirmAnswer = false;

        bool saved = await SaveFolderChoice.ChooseAsync(dialogs, Game(), Account, Provider());

        Assert.False(saved);
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public async Task UnsafeFolderShowsTheProblemAndSavesNothing()
    {
        TestFolder.Write(Path.Combine(Documents, "x.txt"), "x");
        dialogs.FolderToPick = Documents;

        bool saved = await SaveFolderChoice.ChooseAsync(dialogs, Game(), Account, Provider(Documents));

        Assert.False(saved);
        Assert.Contains(dialogs.Calls, call => call.StartsWith("Inform: "));
        Assert.DoesNotContain(dialogs.Calls, call => call.StartsWith("Confirm: "));
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public async Task CancelledPickerSavesNothing()
    {
        dialogs.FolderToPick = null;

        Assert.False(await SaveFolderChoice.ChooseAsync(dialogs, Game(), Account, Provider()));
        Assert.DoesNotContain(dialogs.Calls, call => call.StartsWith("Confirm"));
    }

    [Fact]
    public void UnsafeReasonIsShownInWords()
    {
        var item = new GameItemViewModel(Game(SupportState.Unsafe, nameof(SafetyProblem.ProtectedFolder)), Account,
            Provider());

        Assert.Contains(Strings.SafetyProtected, item.SupportText);
    }

    [Fact]
    public void NotFoundAndUnsureHaveTheirOwnText()
    {
        Assert.Equal(Strings.SupportNotFound, new GameItemViewModel(Game(SupportState.NotFound), Account, Provider()).SupportText);
        Assert.Equal(Strings.SupportUnsure, new GameItemViewModel(Game(SupportState.Unsure), Account, Provider()).SupportText);
    }

    [Fact]
    public void ChooseCommandRunsTheCallback()
    {
        bool called = false;
        var item = new GameItemViewModel(Game(), Account, Provider(), _ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        item.ChooseFolderCommand.Execute(null);

        Assert.True(called);
    }
}
