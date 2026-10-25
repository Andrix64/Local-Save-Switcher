using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class PlayersViewModelTests : IDisposable
{
    private const string Admin = "a1b2c3d4";
    private const string User = "b2c3d4e5";

    private readonly TestFolder folder = new();

    // Chiamate di rimozione: persona e a chi (null = eliminate).
    private readonly List<(string Person, string? GiveTo)> removals = [];

    public PlayersViewModelTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    private People People()
    {
        TestFolder.Write(Path.Combine(folder.Data, "persone.ini"),
            $"Ordine={Admin},{User}\n" +
            $"{Admin}.Nome=Andrea\n{Admin}.Ruolo=admin\n" +
            $"{User}.Nome=Clara\n{User}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();
        return people;
    }

    private PlayersViewModel Model(People people, FakeDialogService dialogs) =>
        new(people, dialogs, (person, giveTo) =>
        {
            removals.Add((person.Id, giveTo));
            return people.Remove(person.Id);
        });

    [Fact]
    public void AddShowsTheNewPlayerAndRefusesADuplicate()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService());

        Assert.True(model.Add("Luca").Success);
        Assert.Equal(3, model.Rows.Count);

        Assert.False(model.Add("luca").Success);
        Assert.True(model.HasMessage);
    }

    [Fact]
    public void TheLastAdminCannotBeDemoted()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService());

        Assert.False(model.SetAdmin(people.Find(Admin)!, false).Success);

        Assert.True(people.Find(Admin)!.IsAdmin);
        Assert.Equal(Strings.LastAdmin, model.Message);
    }

    [Fact]
    public void NextColorTakesTheNextOneOfThePalette()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService());
        Person andrea = people.Find(Admin)!;
        string next = Lss.People.Palette[(Lss.People.Palette.ToList().IndexOf(andrea.Color) + 1) % Lss.People.Palette.Count];

        Assert.True(model.NextColor(andrea).Success);

        Assert.Equal(next, people.Find(Admin)!.Color);
    }

    [Fact]
    public void MoveSwapsThePlayers()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService());

        model.Move(people.Find(User)!, -1);

        Assert.Equal(User, model.Rows[0].Person.Id);
    }

    [Fact]
    public async Task RemovingDeletesThePlaythroughsWhenAskedTo()
    {
        People people = People();
        var dialogs = new FakeDialogService { RemovalAnswer = new RemovalChoice(null) };
        PlayersViewModel model = Model(people, dialogs);

        Result result = await model.RemoveAsync(people.Find(User)!);

        Assert.True(result.Success);
        Assert.Equal([(User, (string?)null)], removals);
    }

    [Fact]
    public async Task RemovingGivesThePlaythroughsToTheChosenPlayer()
    {
        People people = People();
        var dialogs = new FakeDialogService { RemovalAnswer = new RemovalChoice(Admin) };
        PlayersViewModel model = Model(people, dialogs);

        Result result = await model.RemoveAsync(people.Find(User)!);

        Assert.True(result.Success);
        Assert.Equal([(User, (string?)Admin)], removals);
    }

    [Fact]
    public async Task CancelingTheRemovalChangesNothing()
    {
        People people = People();
        var dialogs = new FakeDialogService { RemovalAnswer = null };
        PlayersViewModel model = Model(people, dialogs);

        await model.RemoveAsync(people.Find(User)!);

        Assert.Empty(removals);
        Assert.NotNull(people.Find(User));
    }

    [Fact]
    public async Task TheLastPlayerCannotBeRemoved()
    {
        People people = People();
        people.Remove(User);
        PlayersViewModel model = Model(people, new FakeDialogService { ConfirmAnswer = true });

        Result result = await model.RemoveAsync(people.Find(Admin)!);

        Assert.False(result.Success);
        Assert.Empty(removals);
    }

    [Fact]
    public async Task AddAsksTheNameAndCancelChangesNothing()
    {
        People people = People();
        var dialogs = new FakeDialogService { PromptAnswer = null };
        PlayersViewModel model = Model(people, dialogs);

        await model.AddAsync();
        Assert.Equal(2, model.Rows.Count);

        dialogs.PromptAnswer = "Luca";
        await model.AddAsync();
        Assert.Equal(3, model.Rows.Count);
    }

    [Fact]
    public async Task RenameAsyncUsesTheNewName()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService { PromptAnswer = "Clarissa" });

        await model.RenameAsync(people.Find(User)!);

        Assert.Equal("Clarissa", people.Find(User)!.Name);
    }

    [Fact]
    public async Task OnlyAnAdminGetsAPinAndItCanBeRemoved()
    {
        People people = People();
        var dialogs = new FakeDialogService { PinAnswer = "4821" };
        PlayersViewModel model = Model(people, dialogs);

        await model.SetPinAsync(people.Find(User)!);
        Assert.DoesNotContain("SetPin: Clara", dialogs.Calls);

        await model.SetPinAsync(people.Find(Admin)!);
        Assert.True(people.Find(Admin)!.HasPin);
        Assert.Equal(Strings.PinChangeMenu, model.Rows[0].PinActionText);

        Assert.True(model.RemovePin(people.Find(Admin)!).Success);
        Assert.False(people.Find(Admin)!.HasPin);
    }

    [Fact]
    public void TheRowMarksTheColourOfThePlayer()
    {
        People people = People();
        PlayersViewModel model = Model(people, new FakeDialogService());

        ColourSwatch selected = Assert.Single(model.Rows[0].Swatches, swatch => swatch.Selected);
        Assert.Equal(people.Find(Admin)!.Color, selected.Color);
    }
}
