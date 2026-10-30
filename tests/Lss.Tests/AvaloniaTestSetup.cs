using Avalonia;
using Avalonia.Headless;

namespace Lss.Tests;

/// <summary>
/// Avvia Avalonia senza finestre per i test che usano il dispatcher (il
/// DispatcherTimer dei toast). Una volta sola per processo.
/// </summary>
public sealed class TestApp : Application
{
    private static readonly object Lock = new();
    private static bool started;

    public static void Ensure()
    {
        lock (Lock)
        {
            if (started)
                return;

            AppBuilder.Configure<TestApp>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                .UseSkia()
                .SetupWithoutStarting();

            started = true;
        }
    }
}
