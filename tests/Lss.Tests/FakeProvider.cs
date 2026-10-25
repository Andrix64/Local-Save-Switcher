namespace Lss.Tests;

/// <summary>
/// IGameProvider per i test: account, giochi e stato si impostano dal test.
/// </summary>
public sealed class FakeProvider : IGameProvider
{
    public string Id { get; init; } = "fake";
    public string DisplayName { get; init; } = "Fake";
    public List<GameAccount> Accounts { get; } = [];
    public List<GameInfo> Games { get; } = [];
    public bool CanDetect { get; set; } = true;
    public bool Running { get; set; }
    public DateTime? StartTime { get; set; }
    public bool CanLaunchGames { get; set; } = true;
    public Result LaunchResult { get; set; } = Result.Ok("");
    public List<GameInfo> Launched { get; } = [];
    public bool LauncherRunning { get; set; }
    public CloudState Cloud { get; set; } = CloudState.Enabled;
    public List<SaveRule> Rules { get; } = [];
    public List<RootOverride> Overrides { get; } = [];

    public IReadOnlyList<GameAccount> FindAccounts() => Accounts;
    public IReadOnlyList<GameInfo> FindGames(GameAccount account) => Games;
    public bool CanDetectRunning(GameInfo game) => CanDetect;
    public bool IsGameRunning(GameInfo game) => Running;
    public DateTime? GameStartTime(GameInfo game) => StartTime;
    public bool CanLaunch(GameInfo game) => CanLaunchGames;

    public Result Launch(GameInfo game)
    {
        Launched.Add(game);
        return LaunchResult;
    }

    public bool IsLauncherRunning() => LauncherRunning;
    public CloudState GetCloudState(GameAccount account, GameInfo game) => Cloud;
    public IReadOnlyList<SaveRule> SaveRules(GameAccount account, GameInfo game) => Rules;
    public IReadOnlyList<RootOverride> RootOverrides(GameAccount account, GameInfo game) => Overrides;
}
