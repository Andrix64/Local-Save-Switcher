using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>
/// Validazione del nome nelle finestre di nuova partita e rinomina.
/// </summary>
public sealed class NewPlaythroughViewModelTests
{
    private static NewPlaythroughViewModel New(string existing = "Luigi", string? currentName = null) =>
        new(name => string.Equals(name, existing, StringComparison.OrdinalIgnoreCase), currentName);

    [Fact]
    public void EmptyName_CannotCreate()
    {
        NewPlaythroughViewModel vm = New();

        Assert.False(vm.CanCreate);
        Assert.Equal(Strings.ProfileNameHint, vm.HintText);
        Assert.False(vm.HintIsError);
    }

    [Fact]
    public void InvalidName_DisablesCreateAndShowsHint()
    {
        NewPlaythroughViewModel vm = New();

        vm.Name = "Mario/Peach";

        Assert.False(vm.CanCreate);
        Assert.True(vm.HintIsError);
        Assert.Equal(Loc.F(Strings.InvalidNameHint, Strings.ProfileNameHint), vm.HintText);
    }

    [Fact]
    public void ExistingName_ShowsProfileExists()
    {
        NewPlaythroughViewModel vm = New();

        vm.Name = "Luigi";

        Assert.False(vm.CanCreate);
        Assert.True(vm.HintIsError);
        Assert.Equal(Strings.ProfileExists, vm.HintText);
    }

    [Fact]
    public void ValidNewName_CanCreate()
    {
        NewPlaythroughViewModel vm = New();

        vm.Name = "  Peach  ";

        Assert.True(vm.CanCreate);
        Assert.Equal(new NewPlaythroughResult("Peach", NewContent.Empty, true), vm.CreateResult());
    }

    [Fact]
    public void Rename_SameName_CannotCreate()
    {
        NewPlaythroughViewModel vm = New(currentName: "Luigi");

        Assert.True(vm.IsRename);
        Assert.False(vm.CanCreate);
    }

    [Fact]
    public void Rename_OnlyCaseChanged_CanCreate()
    {
        NewPlaythroughViewModel vm = New(currentName: "Luigi");

        vm.Name = "luigi";

        Assert.True(vm.CanCreate);
        Assert.False(vm.HintIsError);
    }

    [Fact]
    public void ANewPlaythroughStartsEmptyByDefault()
    {
        NewPlaythroughViewModel vm = New();

        Assert.True(vm.EmptyProfile);
        Assert.Equal(NewContent.Empty, vm.Content);
    }

    [Fact]
    public void CopyingNeedsAnExistingPlaythroughAndAName()
    {
        var vm = new NewPlaythroughViewModel(_ => false)
        {
            Sources = [new CopySource("Profilo_Luigi", "Luigi")],
        };
        vm.Name = "Peach";

        vm.CopyOfOption = true;

        Assert.Equal("Profilo_Luigi", vm.SelectedSource?.ProfileName);
        Assert.True(vm.CanCreate);
        Assert.Equal(new NewPlaythroughResult("Peach", NewContent.CopyOf, true, "", "Profilo_Luigi"), vm.CreateResult());
    }

    [Fact]
    public void ImportingNeedsOnlyTheFile()
    {
        NewPlaythroughViewModel vm = New();

        vm.ImportOption = true;
        Assert.False(vm.CanCreate);

        vm.ImportPath = "/salvataggi/luigi.zip";

        Assert.True(vm.CanCreate);
        Assert.False(vm.ShowName);
        Assert.Equal("/salvataggi/luigi.zip", vm.CreateResult().ImportPath);
    }
}
