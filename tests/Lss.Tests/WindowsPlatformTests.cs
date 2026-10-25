namespace Lss.Tests;

/// <summary>
/// Funzioni di WindowsPlatform che non toccano il gioco: girano solo su Windows.
/// </summary>
public sealed class WindowsPlatformTests
{
    [Fact]
    public void FindProcessesUnderEmptyFolderFindsNothing()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var folder = new TestFolder();

        Assert.Empty(new WindowsPlatform().FindProcessesUnder(folder.Root));
        Assert.Empty(new WindowsPlatform().FindProcessesUnder(""));
    }

    [Fact]
    public void MissingRegistryKeyGivesNull()
    {
        if (!OperatingSystem.IsWindows()) return;

        var platform = new WindowsPlatform();

        Assert.Null(platform.ReadRegistryString(@"HKEY_CURRENT_USER\Software\LSS\Inesistente", "Valore"));
        Assert.Empty(platform.RegistrySubKeys(@"HKEY_CURRENT_USER\Software\LSS\Inesistente"));
    }
}
