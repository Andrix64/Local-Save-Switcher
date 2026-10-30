using Lss.App.ViewModels;

namespace Lss.Tests;

/// <summary>Cambiare i file di un gioco Steam con Steam aperto: si chiede di chiuderlo (R-dopo-test sul PC).</summary>
public sealed class SteamGuardTests : IDisposable
{
    private readonly TestFolder folder = new();
    private readonly FakePlatform platform = TestSetup.Fake;

    public SteamGuardTests()
    {
        platform.SteamRunning = false;
        platform.IsSteamGamingMode = false;
        platform.SteamCloses = true;
    }

    public void Dispose()
    {
        platform.SteamRunning = false;
        platform.IsSteamGamingMode = false;
        platform.SteamCloses = true;
        folder.Dispose();
    }

    private Task<bool> Ensure(FakeDialogService dialogs) =>
        SteamGuard.EnsureClosedAsync(dialogs, folder.CreateManager());

    [Fact]
    public async Task WithSteamClosedNothingIsAsked()
    {
        var dialogs = new FakeDialogService();

        Assert.True(await Ensure(dialogs));
        Assert.Empty(dialogs.Calls);
    }

    [Fact]
    public async Task ClosingSteamClosesItAndGoesOn()
    {
        platform.SteamRunning = true;
        var dialogs = new FakeDialogService { ChooseAnswer = 0 };
        int before = platform.CloseSteamCalls;

        Assert.True(await Ensure(dialogs));
        Assert.Equal(before + 1, platform.CloseSteamCalls);
        Assert.False(platform.SteamRunning);
    }

    [Fact]
    public async Task CancellingStopsTheChange()
    {
        platform.SteamRunning = true;

        Assert.False(await Ensure(new FakeDialogService { ChooseAnswer = -1 }));
    }

    [Fact]
    public async Task IfSteamDoesNotCloseTheChangeStopsAndTheUserIsTold()
    {
        platform.SteamRunning = true;
        platform.SteamCloses = false;
        var dialogs = new FakeDialogService { ChooseAnswer = 0 };

        Assert.False(await Ensure(dialogs));
        Assert.Contains(Strings.SteamCloseFailed, dialogs.LastInformText);
    }

    [Fact]
    public async Task InGamingModeSteamIsClosedToo()
    {
        platform.SteamRunning = true;
        platform.IsSteamGamingMode = true;
        var dialogs = new FakeDialogService { ChooseAnswer = 0 };
        int before = platform.CloseSteamCalls;

        Assert.True(await Ensure(dialogs));
        Assert.Equal(before + 1, platform.CloseSteamCalls);
    }
}
