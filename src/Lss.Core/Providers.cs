namespace Lss;

/// <summary>
/// I launcher usati da LSS. Per ora solo Steam; gli altri si aggiungono qui.
/// </summary>
public static class Providers
{
    public static IReadOnlyList<IGameProvider> All(IPlatform platform) =>
        [new SteamProvider(platform)];
}
