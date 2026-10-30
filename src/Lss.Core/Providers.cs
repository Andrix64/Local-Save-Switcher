namespace Lss;

/// <summary>
/// I launcher usati da LSS, nell'ordine della lista. Se lo stesso gioco compare
/// in due launcher, vince il primo (vedi DuplicateGames).
/// </summary>
public static class Providers
{
    public static IReadOnlyList<IGameProvider> All(IPlatform platform) =>
        [new SteamProvider(platform), new EpicProvider(platform), new GogProvider(platform), new HeroicProvider(platform), new ManualProvider(platform)];
}
