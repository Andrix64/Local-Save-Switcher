using Lss.App.ViewModels;

namespace Lss.Tests;

public sealed class SimplePlayFlowTests : IDisposable
{
    private const string Second = "b2c3d4e5";

    private readonly TestFolder folder = new();

    public SimplePlayFlowTests() => TestApp.Ensure();

    public void Dispose() => folder.Dispose();

    // Manager con il provider finto (lancio e cloud) e due persone: Andrea admin, Clara utente.
    private (SaveManager Manager, People People, FakeProvider Provider) Setup(FakeProvider? provider = null)
    {
        provider ??= new FakeProvider { Cloud = CloudState.Disabled };
        TestFolder.Write(folder.SaveFile, "A1");
        TestFolder.Write(folder.CacheFile, "cache");
        TestFolder.WritePeople(folder.Data,
            $"Ordine={TestFolder.Owner},{Second}\n" +
            $"{TestFolder.Owner}.Nome=Andrea\n{TestFolder.Owner}.Ruolo=admin\n" +
            $"{Second}.Nome=Clara\n{Second}.Ruolo=utente\n");

        var people = new People(folder.Data);
        people.Load();

        var manager = new SaveManager(folder.Game, folder.Data, "", null, provider) { Players = people };
        return (manager, people, provider);
    }

    private static GameItemViewModel Item(FakeProvider provider) =>
        new(new GameInfo("fake", "1", "Mario", "", "", "", [], SupportState.Supported, Source: SaveSource.None),
            new GameAccount("fake", "x", "Fake", "", null), provider);

    private static Task<Result> Ready() => Task.FromResult(Result.Ok(""));

    [Fact]
    public async Task ACloudGameIsBlockedForAUser()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Enabled });
        var dialogs = new FakeDialogService { InformAnswer = false, ChosenAdmin = null };
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(Second)!, Ready);

        Assert.Null(session);
        Assert.Empty(provider.Launched);
        Assert.False(manager.State.CloudAllowedInSimple);
    }

    [Fact]
    public async Task AnAdminAllowsACloudGameAndTheConsentIsWritten()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Enabled });
        var dialogs = new FakeDialogService { ChooseAnswer = 0 };
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(TestFolder.Owner)!, Ready);

        Assert.NotNull(session);
        Assert.True(manager.State.CloudAllowedInSimple);
        Assert.Single(provider.Launched);
    }

    [Fact]
    public async Task CancelingTheAdminQuestionStartsNothing()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Enabled });
        var dialogs = new FakeDialogService { ChooseAnswer = -1 };
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(TestFolder.Owner)!, Ready);

        Assert.Null(session);
        Assert.Empty(provider.Launched);
        Assert.False(manager.State.CloudAllowedInSimple);
    }

    [Fact]
    public async Task WithoutCloudThereIsNoQuestion()
    {
        var (manager, people, provider) = Setup();
        var dialogs = new FakeDialogService();
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(Second)!, Ready);

        Assert.NotNull(session);
        Assert.Empty(dialogs.Calls);
    }

    [Fact]
    public async Task AFailedPreparationIsShownAndNothingStarts()
    {
        var (manager, people, provider) = Setup();
        var dialogs = new FakeDialogService();
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(Second)!,
            () => Task.FromResult(Result.Fail("niente")));

        Assert.Null(session);
        Assert.Empty(provider.Launched);
        Assert.Single(dialogs.Calls);
    }

    [Fact]
    public async Task AFailedLaunchIsShown()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Disabled, LaunchResult = Result.Fail("boom") });
        var dialogs = new FakeDialogService();
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(Second)!, Ready);

        Assert.Null(session);
        Assert.Single(dialogs.Calls);
    }

    [Fact]
    public async Task WhenTheLauncherCannotLaunchTheSessionSaysSo()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Disabled, CanLaunchGames = false });
        var flow = new SimplePlayFlow(new FakeDialogService(), people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(Second)!, Ready);

        Assert.NotNull(session);
        Assert.False(session!.Launched);
        Assert.Empty(provider.Launched);
    }

    [Fact]
    public async Task FinishingSavesAndCountsThePlayTime()
    {
        var (manager, people, provider) = Setup();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        Assert.True(manager.ActivateProfile("Profilo_Mario").Success);
        var flow = new SimplePlayFlow(new FakeDialogService(), people);
        PlaySession session = (await flow.StartAsync(manager, Item(provider), people.Find(TestFolder.Owner)!, Ready))!;

        Result done = await flow.FinishAsync(session);

        Assert.True(done.Success, done.Message);
        Assert.NotNull(manager.GetInfo("Profilo_Mario").LastSession);
    }

    [Fact]
    public async Task OnlyThisTimeStartsTheGameWithoutWritingTheConsent()
    {
        var (manager, people, provider) = Setup(new FakeProvider { Cloud = CloudState.Enabled });
        var dialogs = new FakeDialogService { ChooseAnswer = 1 };
        var flow = new SimplePlayFlow(dialogs, people);

        PlaySession? session = await flow.StartAsync(manager, Item(provider), people.Find(TestFolder.Owner)!, Ready);

        Assert.NotNull(session);
        Assert.Single(provider.Launched);
        Assert.False(manager.State.CloudAllowedInSimple);
    }
    [Fact]
    public async Task NothingSavedInTheGameIsExplainedWithoutBlamingTheAdmin()
    {
        var (manager, people, provider) = Setup();
        manager.CreateProfile("Mario", true, TestFolder.Owner);
        Assert.True(manager.ActivateProfile("Profilo_Mario").Success);
        var dialogs = new FakeDialogService();
        var flow = new SimplePlayFlow(dialogs, people);
        PlaySession session = (await flow.StartAsync(manager, Item(provider), people.Find(TestFolder.Owner)!, Ready))!;

        // Il gioco non ha salvato niente: la cartella dei salvataggi è vuota alla chiusura.
        Directory.Delete(Path.GetDirectoryName(folder.SaveFile)!, true);

        Result done = await flow.FinishAsync(session);

        Assert.False(done.Success);
        Assert.Contains(Strings.SimpleNothingSaved, dialogs.LastInformText);
        Assert.Contains(Strings.SimpleNothingSavedWarning, dialogs.LastInformText);
        Assert.DoesNotContain(Strings.SimpleAskForHelp, dialogs.LastInformText);
    }

    // Un gioco lento a partire non è un gioco finito: l'attesa non finisce da sola, ma avvisa.
    [Fact]
    public async Task AGameThatIsNeverSeenIsWaitedForAndFlaggedOverdueOnce()
    {
        int overdue = 0;
        using var cancel = new CancellationTokenSource();

        Task waiting = SimplePlayFlow.WaitWhileRunningAsync(() => false, TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(10), () => overdue++, cancel.Token);

        await Task.Delay(300);

        Assert.False(waiting.IsCompleted);
        Assert.Equal(1, overdue);

        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task AGameSeenAfterAWhileIsFollowedUntilItCloses()
    {
        int checks = 0;
        int overdue = 0;

        // Non gira per 20 controlli, poi gira per 5, poi si chiude.
        bool IsRunning()
        {
            int n = Interlocked.Increment(ref checks);
            return n is > 20 and <= 25;
        }

        await SimplePlayFlow.WaitWhileRunningAsync(IsRunning, TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMinutes(1), () => overdue++, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(checks >= 26);
        Assert.Equal(0, overdue);
    }
}
